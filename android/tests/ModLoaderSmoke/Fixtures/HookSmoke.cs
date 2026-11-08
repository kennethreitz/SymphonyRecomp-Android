using System;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

// Desktop-only regression fixture. Android must reject attribute-hook mods.
public static class HookSmoke
{
    [PreHook("main", "VSync")]
    public static void BeforeVSync(CpuContext cpu, IMemory memory) => Console.WriteLine("[DesktopHookSmoke] GAME_HOOK_OK main.VSync");
    [PreHook("dra", "MainGame")]
    public static void BeforeMainGame(CpuContext cpu, IMemory memory) => Console.WriteLine("[DesktopHookSmoke] GAME_HOOK_OK dra.MainGame");
}
