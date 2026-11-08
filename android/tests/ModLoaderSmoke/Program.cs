using System.Runtime.CompilerServices;
using System.IO.Compression;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Dispatch;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

// This tests the shared loader on macOS/CoreCLR, NOT Android/Mono compatibility.
var repository = Path.GetFullPath(args[0]);
var mode = args.Length > 1 ? args[1] : "load";
var temp = Path.Combine(Path.GetTempPath(), "sotn-mod-loader-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
Settings.RootDirectory = temp;
var mods = Path.Combine(temp, "mods");
Directory.CreateDirectory(mods);
ModLoader.DefaultRoot = mods;
foreach (var folder in Directory.GetDirectories(Path.Combine(repository, "android/mods/dist")))
{
    var dest = Path.Combine(mods, Path.GetFileName(folder));
    if (mode == "zip") ZipFile.CreateFromDirectory(folder, dest + ".zip");
    else
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(folder)) File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
    }
}
var hookFixture = Path.Combine(mods, "desktop-hook-smoke");
Directory.CreateDirectory(hookFixture);
File.WriteAllText(Path.Combine(hookFixture, "mod.json"), "{\"id\":\"desktop-hook-smoke\",\"version\":\"1.0\",\"defaultEnabled\":true}");
File.Copy(Path.Combine(repository, "android/tests/ModLoaderSmoke/Fixtures/HookSmoke.cs"), Path.Combine(hookFixture, "HookSmoke.cs"));
if (mode is "disable" or "script-disabled")
    File.WriteAllText(Path.Combine(mods, "mod-settings.json"), "{\"enabled\":false}");
else if (mode == "select")
    File.WriteAllText(Path.Combine(mods, "mod-settings.json"), "{\"mods\":{\"android-mod-smoke\":false}}");
else if (mode == "invalid")
    File.WriteAllText(Path.Combine(mods, "mod-settings.json"), "{not-json");
if (mode == "bad-dll")
{
    var json = Path.Combine(mods, "android-mod-smoke/mod.json");
    File.WriteAllText(json, File.ReadAllText(json).Replace("AndroidModSmoke.dll", "../AndroidModSmoke.dll"));
}
if (mode == "bad-id")
{
    var json = Path.Combine(mods, "android-mod-smoke/mod.json");
    File.WriteAllText(json, File.ReadAllText(json).Replace("android-mod-smoke", "../bad"));
}
Dispatcher.Register("main", new TestOverlay("main", Targets.VSync));
Dispatcher.Register("dra", new TestOverlay("dra", Targets.MainGame));
if (mode == "script-disabled" && ModLoader.LoadScripts("smoke", Path.Combine(repository, "android/mods/SourceSmoke")))
    throw new Exception("Global disable must also prevent translation/script extensions loading before LoadAll");
ModLoader.LoadAll();
var enabled = ModLoader.Mods.Where(m => m.Loaded).ToArray();
var expected = mode is "disable" or "invalid" or "script-disabled" ? 0 : mode is "select" or "bad-dll" or "bad-id" ? 2 : 3;
if (enabled.Length != expected)
    throw new Exception($"Expected {expected} loaded mods, got {enabled.Length}: " + string.Join("; ", ModLoader.Mods.Select(m => m.LoadError)));
var cpu = new CpuContext();
var memory = new PSMemory(0x00800000);
Targets.VSync(cpu, memory);
Targets.MainGame(cpu, memory);
Event.Dispatch(new VSyncEvent());
Console.WriteLine($"MOD_LOADER_SMOKE_OK mode={mode} loaded={enabled.Length} installedDetours={HookManager.HookedFunctionCount}");
Console.WriteLine($"Test artifacts retained at {temp}");

sealed class TestOverlay(string name, Action<CpuContext, IMemory> target) : IOverlay
{
    public string Name => name;
    public IReadOnlyDictionary<uint, Action<CpuContext, IMemory>> Functions { get; } =
        new Dictionary<uint, Action<CpuContext, IMemory>> { [1] = target };
}
static class Targets
{
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void VSync(CpuContext cpu, IMemory memory) { }
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static void MainGame(CpuContext cpu, IMemory memory) { }
}
