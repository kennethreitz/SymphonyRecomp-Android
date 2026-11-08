using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;

namespace SymphonyRecomp.Android.SecondScreen;

// Hosts the castle map on the Thor's lower panel.
//
// Presentation is Android's own API for driving a secondary display, and the
// Thor's Screen-2 advertises FLAG_PRESENTATION, so this is the supported route
// rather than a trick. It gets its own layerStack, so nothing here touches the
// game's surface or its EGL context.
public sealed class MapPresentation : Presentation
{
    // The map only changes as the player crosses room boundaries, so redrawing
    // at 10Hz is imperceptible and costs nothing next to the 60fps game.
    const int RefreshMs = 100;

    CastleMapView? _view;
    Handler? _handler;
    Java.Lang.Runnable? _tick;

    public MapPresentation(Context outerContext, Display display)
        : base(outerContext, display)
    {
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _view = new CastleMapView(Context!);

        // Explicit MatchParent: a bare View defaults to WrapContent and measures
        // to 0x0, so it occupies no space and never draws.
        SetContentView(_view, new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        _handler = new Handler(Looper.MainLooper!);
        _tick = new Java.Lang.Runnable(() =>
        {
            _view?.Invalidate();
            if (_tick != null) _handler?.PostDelayed(_tick, RefreshMs);
        });
    }

    protected override void OnStart()
    {
        base.OnStart();
        if (_tick != null) _handler?.PostDelayed(_tick, RefreshMs);
    }

    protected override void OnStop()
    {
        // Stop redrawing when the panel is not showing, so a backgrounded app is
        // not waking up ten times a second to draw nothing.
        if (_tick != null) _handler?.RemoveCallbacks(_tick);
        base.OnStop();
    }
}
