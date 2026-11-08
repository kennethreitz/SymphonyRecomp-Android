using System.Runtime.CompilerServices;
using MonoMod.RuntimeDetour;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Modding;

public sealed class AndroidModSmoke : IMod
{
    private Hook? _probeHook;
    private static int _eventSeen;

    public void OnLoad()
    {
        Console.WriteLine("[AndroidModSmoke] DLL_LOADED AndroidModSmoke v1");
        Event.AddListener<VSyncEvent>(OnVSync);
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
    }

    public void OnUnload()
    {
        Event.RemoveListener<VSyncEvent>(OnVSync);
        _probeHook?.Dispose();
    }
}
