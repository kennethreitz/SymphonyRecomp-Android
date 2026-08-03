# SymphonyRecomp — Android

Android port targeting the **AYN Thor** (Snapdragon 8 Gen 2 / Adreno 740). Boots
straight into gameplay with widescreen — no menus, no mod system, no debug UI.

## Prerequisites

- .NET 10 SDK + `android` workload (`dotnet workload install android`)
- JDK 17+
- Android SDK (platform 36)
- A legally owned PSX disc rip, same three files the desktop build wants

## Build

The recompiler is a **desktop** build step. It reads the disc and emits C# into
`generated/`, which the Android head project then compiles in. You cannot skip
this — without `generated/` the head project fails on `Entry`, `Bios` and `SoTN`.

```bash
# 1. Disc files go in disc/ at the repo root (same names as the desktop build)

# 2. Generate the recompiled C# (desktop tool, run once per disc)
dotnet run --project RecompOne/RecompOne.Recompiler config/sotn.json

# 3. Build the APK
dotnet build android/SymphonyRecomp.Android -c Release
```

Release enables AOT. That is only possible because the mod system is excluded —
Roslyn and MonoMod both need a JIT, and nothing else in the runtime does.

## Installing the disc

The app looks for the rip in its external files dir:

```bash
adb shell mkdir -p /sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/disc
adb push "Castlevania - Symphony of the Night (USA).cue" \
         "Castlevania - Symphony of the Night (Track 1).bin" \
         "Castlevania - Symphony of the Night (Track 2).bin" \
         /sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/disc/
```

It also honours an explicit `CdPath` in the game config if you set one. With no
UI there is no picker, so a missing disc reports to logcat and stops:

```bash
adb logcat -s SymphonyRecomp DOTNET
```

## What differs from desktop

| Area | Desktop | Android |
|---|---|---|
| Window | Silk.NET windowing (GLFW), GL 4.5 core | `SurfaceView` + EGL directly, GLES 3.2 |
| Blending | Dual-source (`Src1Color`/`Src1Alpha`) | `EXT_shader_framebuffer_fetch`, blend in-shader |
| VRAM hazard | `TextureBarrier` | Not needed — shader reads its own destination |
| Audio | OpenAL Soft | `AudioTrack` |
| Input | SDL GameController | Android key/motion events |
| UI | ImGui panels, menus, settings | None |
| Mods | Roslyn + MonoMod detours | Excluded, which is what allows AOT |

`Silk.NET.Windowing.Sdl.Android` was the obvious route and is **not usable** — it
last shipped 2.10.1 in Nov 2021, targets `MonoAndroid9.0`, and pins Silk.NET to
2.10.1 against the 2.23 the runtime uses. Hence the direct EGL path.

## Render scale

`ViewConfig.RenderScale` replaces the old 1×/4× `NativeResolution` toggle and
accepts 1–8. Cost is quadratic in VRAM texture area (1024×512 × scale²), so 8×
is roughly 4× the memory of 4×. The 8 Gen 2 has room; the default is 4.

## Config keys

Read from the view config; all have working defaults, so none are required.

| Key | Default | Meaning |
|---|---|---|
| `RenderScale` | `4` | Internal resolution, 1–8× native |
| `WidescreenOriginalAspect` | `false` | `true` forces 4:3 everywhere |
| `WidescreenAspect` | `1.777` | Target aspect when widescreen is on |
| `SwitchButtonLayout` | `true` | Nintendo face-button labels; `false` for Xbox-style |
| `StickAsDpad` | `true` | Left stick doubles as directional input |

Widescreen is enabled at startup by `MainActivity` because the ImGui settings
section that normally toggles it is not compiled on Android.

## Verified on hardware

Tested on an AYN Thor (Snapdragon 8 Gen 2 / Adreno 740, Android 13):

- Boots from disc into gameplay, **60.0fps locked**, JIT only, no AOT
- GLES 3.2 via EGL; `EXT_shader_framebuffer_fetch` blending correct in real
  content — dialogue overlays, moon glow, mist all render properly
- Widescreen 16:9 on stages, re-evaluated per room load
- Gamepad, d-pad, hat and left stick; rumble via `Vibrator`
- `glGetError` clean every frame

## Known issues

- **Prologue is pinned to 4:3.** Its art does not extend past 4:3, so a wider
  viewport shows past the edge of the set. See `WidescreenPatch.NarrowStages` —
  add other stages there if they show the same.
- **Audio is untested.** The `AudioTrack` sink has never been listened to.
- **A single-track rip has no CDDA.** Most SoTN music is XA streamed from the
  data track and should be fine, but anything played as redbook audio has
  nothing to read. Needs a two-track rip.
- **Possible mirroring in the 24-bit MDEC path.** Text on the intro map still
  looked horizontally mirrored; unconfirmed whether that is the original art.
  `GlesShaders.Present24Fs` is where to look.
