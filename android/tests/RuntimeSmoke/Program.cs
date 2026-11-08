global using Silk.NET.OpenGL;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Hle;

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
Settings.RootDirectory = output;
void Check(bool ok, string what) { if (!ok) throw new Exception(what); }
if (args.Contains("--legacy"))
{
    File.WriteAllText(Path.Combine(output, "settings.json"), """
        {"CdPath":"disc/game.cue","CardAPath":"saves/slot-a.sav","CardBPath":"saves/slot-b.sav",
         "CardAEnabled":true,"CardBEnabled":false,"MasterVolume":0.75,"Muted":false,
         "ActiveMods":["example"]}
        """);
    File.WriteAllText(Path.Combine(output, "interface.ini"), "[RecompOne]\nRenderScale=3\nWidescreenOriginalAspect=false\nSwitchButtonLayout=false\n");
}
Settings.EnsureLoaded();
Check(Settings.System.DiscPath == "disc/game.cue", "disc migration");
Check(Settings.System.CardA.Path == "saves/slot-a.sav", "card A path migration");
Check(Settings.System.CardB.Path == "saves/slot-b.sav" && !Settings.System.CardB.Enabled, "card B migration");
Check(Settings.System.Audio.Master == 0.75f, "audio migration");
Check(DisplayOptions.RenderScale.Value == 3, "render scale migration");
Check(!Settings.Get("widescreen.original_aspect", true), "widescreen migration");
Check(!Settings.Get("android.switch_button_layout", true), "input migration");
Check(Settings.Get("mods.example.enabled", false), "mod preference migration");
Settings.Save();
Check(File.Exists(Path.Combine(output, "settings.json.pre-upstream.bak")), "original config backup");
Check(File.ReadAllText(Path.Combine(output, "interface.ini")).Contains("RenderScale=3"), "legacy ini preserved");

File.WriteAllText(Path.Combine(output, "prim.vert"), GlesShaders.PrimVs);
File.WriteAllText(Path.Combine(output, "prim.frag"), GlesShaders.PrimFs);
File.WriteAllText(Path.Combine(output, "present.vert"), GlesShaders.FullscreenVs);
File.WriteAllText(Path.Combine(output, "present.frag"), GlesShaders.PresentFs);
File.WriteAllText(Path.Combine(output, "present24.frag"), GlesShaders.Present24Fs);
Check(GlesShaders.PrimVs.Contains("in vec3  inColorF"), "upstream float vertex ABI");
Check(GlesShaders.PrimFs.Contains("GL_EXT_shader_framebuffer_fetch"), "framebuffer fetch");
Check(!GlesShaders.PrimFs.Contains("index = 1"), "no desktop dual-source output");
Check(!GlesShaders.PrimFs.Contains("texelFetch(uDest"), "no destination feedback loop");
Check(GlesShaders.PrimFs.Contains("uBlendMode == 2 && BlendColor.a != 0.0"), "subtractive mode must not subtract opaque texels");
Console.WriteLine("PASS: settings migration/reload, save paths, input, upstream GLES shader contract");
