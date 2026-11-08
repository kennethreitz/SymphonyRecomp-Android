using Android.App;
using Android.Content.PM;
using Android.Hardware.Display;
using Android.OS;
using Android.Views;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Host;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Platform.Android;
using Recompiled;
using SymphonyRecomp.Android.SecondScreen;

namespace SymphonyRecomp.Android;

// Boots straight into gameplay. No launcher, no menus - the disc path comes from
// config and widescreen is applied before the game thread starts.
[Activity(
    Label = "SymphonyRecomp",
    MainLauncher = true,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize |
                           ConfigChanges.KeyboardHidden | ConfigChanges.Keyboard,
    LaunchMode = LaunchMode.SingleTask)]
public sealed class MainActivity : Activity, ISurfaceHolderCallback
{
    SurfaceView? _surfaceView;
    Thread? _gameThread;
    volatile bool _started;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // First thing: without this the runtime's Console output goes nowhere and
        // a startup failure is invisible.
        LogcatWriter.Install();
        Console.WriteLine("[Activity] OnCreate");

        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);

        _surfaceView = new SurfaceView(this);
        _surfaceView.Holder!.AddCallback(this);
        SetContentView(_surfaceView);

        // After SetContentView: the DecorView does not exist until the content
        // view is set, and InsetsController comes off the DecorView.
        GoImmersive();

        ShowMapOnSecondScreen();
    }

    void GoImmersive()
    {
        if (Window == null) return;

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            Window.SetDecorFitsSystemWindows(false);
            // Still null if called before the DecorView is realised.
            var controller = Window.DecorView?.WindowInsetsController;
            if (controller != null)
            {
                controller.Hide(WindowInsets.Type.SystemBars());
                controller.SystemBarsBehavior =
                    (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }
        else
        {
#pragma warning disable CA1422 // SystemUiFlags is the only route below API 30
            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)(
                SystemUiFlags.ImmersiveSticky | SystemUiFlags.Fullscreen |
                SystemUiFlags.HideNavigation | SystemUiFlags.LayoutStable |
                SystemUiFlags.LayoutFullscreen | SystemUiFlags.LayoutHideNavigation);
#pragma warning restore CA1422
        }
    }

    // --- Surface lifecycle -------------------------------------------------

    public void SurfaceCreated(ISurfaceHolder holder)
    {
        Console.WriteLine("[Activity] SurfaceCreated");
        HostWindow.AttachSurface(this, holder);
        StartGameThread();
    }

    public void SurfaceChanged(ISurfaceHolder holder, global::Android.Graphics.Format format, int width, int height)
        => HostWindow.AttachSurface(this, holder);

    // The EGL context outlives this; only the window surface goes away.
    public void SurfaceDestroyed(ISurfaceHolder holder) => HostWindow.DetachSurface();

    // --- Second screen -----------------------------------------------------

    MapPresentation? _mapPresentation;

    // The Thor's lower panel reports as a separate Display with FLAG_PRESENTATION.
    // Anything else (single-screen device, no secondary display) is a no-op.
    void ShowMapOnSecondScreen()
    {
        try
        {
            var manager = (DisplayManager?)GetSystemService(DisplayService);
            var displays = manager?.GetDisplays(DisplayManager.DisplayCategoryPresentation);
            if (displays == null || displays.Length == 0)
            {
                Console.WriteLine("[Map] no presentation display, second screen skipped");
                return;
            }

            var target = displays[0];
            Console.WriteLine($"[Map] using display {target.DisplayId} '{target.Name}'");

            _mapPresentation = new MapPresentation(this, target);
            _mapPresentation.Show();
        }
        catch (Exception e)
        {
            // A failure here must never take the game down with it.
            Console.Error.WriteLine($"[Map] second screen unavailable: {e.Message}");
            _mapPresentation = null;
        }
    }

    // --- Game thread -------------------------------------------------------

    void StartGameThread()
    {
        if (_started) return;
        _started = true;

        // Its own thread because the EGL context is made current on whichever
        // thread drives GL, and that must not be the UI thread.
        _gameThread = new Thread(RunGame)
        {
            IsBackground = true,
            Name = "sotn-game",
        };
        _gameThread.Start();
    }

    void RunGame()
    {
        try
        {
            Console.WriteLine("[Activity] game thread start");

            // Must precede any config or memory-card access: the working
            // directory on Android is "/" and is not writable.
            var files = FilesDir?.AbsolutePath;
            if (files != null)
            {
                Settings.RootDirectory = files;
                Console.WriteLine($"[Activity] config root = {files}");
            }

            var modRoot = Path.Combine(GetExternalFilesDir(null)?.AbsolutePath ?? files
                ?? throw new InvalidOperationException("No writable app files directory"), "mods");
            RecompOne.Runtime.Modding.ModLoader.DefaultRoot = modRoot;
            Directory.CreateDirectory(modRoot);
            var packRoot = Path.Combine(Path.GetDirectoryName(modRoot)!, "packs");
            RecompOne.Runtime.Assets.AssetReplacerManager.Instance.DefaultRoot = packRoot;
            Directory.CreateDirectory(packRoot);
            Console.WriteLine($"[Activity] packs = '{packRoot}'");
            Console.WriteLine($"[Activity] mods = '{modRoot}'");
            ResolveDiscPath();
            Console.WriteLine($"[Activity] disc = '{Settings.System.DiscPath}'");
            WidescreenPatch.Register();

            // The upstream options replace Runtime.View. Preserve user preferences,
            // while giving new Android installs the port's usual 16:9 default.
            if (!WidescreenOptions.OriginalAspect.IsSet) WidescreenOptions.OriginalAspect.Value = false;
            WidescreenPatch.Refresh();
            TextureSourcesPatch.Register();
            RoomFills.Register();
            Recompiled.Translation.GameLanguage.Register();

            Console.WriteLine("[Activity] widescreen registered, entering Entry.Run");

            var mem = new PSMemory(0x00800000); // Upstream room extensions use 8 MiB RAM.
            RecompOne.Runtime.Runtime.Run(() => Entry.Run(mem, Settings.System.DiscPath, "SymphonyRecomp"));
            Console.WriteLine("[Activity] Entry.Run returned");
        }
        catch (Exception e)
        {
            // Surfaces in logcat under the SymphonyRecomp tag. Without a UI there
            // is nowhere else for a startup failure to go.
            Console.Error.WriteLine($"[SymphonyRecomp] fatal: {e}");
        }
    }

    // Looks for the disc next to the app's external files dir, which is what
    // `adb push` and any file manager can reach without special permissions.
    void ResolveDiscPath()
    {
        var configured = Settings.System.DiscPath;
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return;

        var root = GetExternalFilesDir(null)?.AbsolutePath;
        if (root == null) return;

        var cue = Path.Combine(root, "disc", "Castlevania - Symphony of the Night (USA).cue");
        if (File.Exists(cue))
        {
            Settings.System.DiscPath = cue;
            Settings.Save();
            return;
        }

        Console.Error.WriteLine(
            $"[SymphonyRecomp] no disc found. Push the rip to {Path.Combine(root, "disc")}/");
    }

    // --- Input -------------------------------------------------------------

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e != null && (e.Action == KeyEventActions.Down || e.Action == KeyEventActions.Up))
        {
            if (AndroidInput.HandleKey(e.KeyCode, e.Action == KeyEventActions.Down))
                return true;
        }
        return base.DispatchKeyEvent(e);
    }

    public override bool DispatchGenericMotionEvent(MotionEvent? e)
    {
        if (e != null && AndroidInput.HandleMotion(e)) return true;
        return base.DispatchGenericMotionEvent(e);
    }

    // --- Activity lifecycle ------------------------------------------------

    protected override void OnResume()
    {
        base.OnResume();
        GoImmersive();
        if (_mapPresentation == null) ShowMapOnSecondScreen();
    }

    protected override void OnPause()
    {
        try { _mapPresentation?.Dismiss(); } catch { /* display already gone */ }
        _mapPresentation = null;
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        try { _mapPresentation?.Dismiss(); } catch { /* display already gone */ }
        _mapPresentation = null;
        HostWindow.RequestClose();
        base.OnDestroy();
    }
}
