using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Assets.Textures;
using RecompOne.Runtime.Modding;

public sealed class AndroidModSmoke : IMod
{
    private Hook? _probeHook;
    private static int _eventSeen, _frames;
    private static bool _hudReported;

    public void OnLoad()
    {
        Console.WriteLine("[AndroidModSmoke] DLL_LOADED AndroidModSmoke v1");
        Event.AddListener<VSyncEvent>(OnVSync);
        TextureRegistry.Collect(this, true);
        try
        {
            var before = Probe();
            _probeHook = new Hook(typeof(AndroidModSmoke).GetMethod(nameof(Probe))!,
                (Func<Func<int>, int>)(original => original() + 1));
            var after = Probe();
            Console.WriteLine(before == 41 && after == 42
                ? "[AndroidModSmoke] DETOUR_OK before=41 after=42"
                : $"[AndroidModSmoke] DETOUR_INCORRECT before={before} after={after}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AndroidModSmoke] DETOUR_UNAVAILABLE {ex}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static int Probe() => 41;

    private static void OnVSync(VSyncEvent evt)
    {
        if (Interlocked.Exchange(ref _eventSeen, 1) == 0)
            Console.WriteLine("[AndroidModSmoke] EVENT_OK VSyncEvent (no detour required)");
        if (!_hudReported && _frames % 120 == 0)
        {
            var hud = TextureRegistry.Snapshot().Where(t => t.Group == "hud").Take(16).ToArray();
            foreach (var texture in hud)
                Console.WriteLine($"[AndroidModSmoke] HUD_TEXTURE index={texture.IndexHash:x16} clut={texture.ClutHash:x16} {texture.W}x{texture.H} tp={texture.TPage}");
            _hudReported = hud.Length > 0;
        }
        if (++_frames is 180 or 600)
            foreach (var texture in TextureRegistry.Snapshot().Where(t => !t.Dynamic)
                .OrderByDescending(t => t.W * t.H).Take(24))
                Console.WriteLine($"[AndroidModSmoke] TEXTURE index={texture.IndexHash:x16} clut={texture.ClutHash:x16} {texture.W}x{texture.H} tp={texture.TPage} group={texture.Group}");
    }

    public void OnUnload()
    {
        Event.RemoveListener<VSyncEvent>(OnVSync);
        TextureRegistry.Collect(this, false);
        _probeHook?.Dispose();
    }
}
