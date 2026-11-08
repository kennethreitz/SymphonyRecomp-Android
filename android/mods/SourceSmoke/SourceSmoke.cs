using System;
using System.Threading;
using RecompOne.Runtime.Modding;
using RecompOne.Runtime.Events;

public sealed class AndroidSourceSmoke : IMod
{
    private static int seen;
    public void OnLoad()
    {
        Console.WriteLine("[AndroidSourceSmoke] SOURCE_COMPILED_AND_LOADED");
        Event.AddListener<VSyncEvent>(OnVSync);
    }
    private static void OnVSync(VSyncEvent evt)
    {
        if (Interlocked.Exchange(ref seen, 1) == 0)
            Console.WriteLine("[AndroidSourceSmoke] SOURCE_EVENT_OK");
    }
    public void OnUnload() => Event.RemoveListener<VSyncEvent>(OnVSync);
}
