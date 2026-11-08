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

## November 2026 upstream sync

The `sync/upstream-2026-11-08` branch integrates upstream's 1.0 pre-release
code while keeping the Android EGL/AudioTrack/input host and second-screen
sources. It is not hardware-validated and has not been installed.

- Android now uses the new `Settings`/`Option` API and `Runtime.Run` GPU-owner
  thread, with 8 MiB RAM like the updated desktop head.
- `Settings.RootDirectory` keeps settings and relative memory-card paths inside
  the app's files directory. Legacy `settings.json` is backed up to
  `settings.json.pre-upstream.bak` before conversion. Disc/card paths, card
  enablement, audio, keyboard/pad bindings, and known Android/widescreen options
  are imported. `interface.ini` and memory-card files are not removed.
- GLES shaders share upstream's float vertex layout and replacement-texture
  paths. Framebuffer-fetch blending, 5551 pixel conversion, direct screen blit,
  and Adreno present-FBO reattachment are retained.
- Desktop Slang post-processing/UI-layer extraction remains disabled on GLES.
- The conservative 4:3 prologue gate remains until tested with new room art.
- Upstream room extensions/fills and translation data paths are registered.
  Translation packs containing C# extensions still need the mod system.
- The Android mod loader remains a no-op. No code-mod support was enabled.

On this Mac, the pre-existing Android workload is in `$HOME/.dotnet`, not the
system `dotnet`. Build using that SDK and the existing SDK/JDK paths:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:/opt/homebrew/bin:$PATH"
export ANDROID_HOME="$HOME/android-sdk"
export JAVA_HOME=/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home

dotnet run --project RecompOne/RecompOne.Recompiler -- config/sotn.json
dotnet build android/SymphonyRecomp.Android -c Release \
  -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME"
dotnet build RecompOne.SoTN.csproj -c Release
```

Regression smoke tests (use a new disposable output directory, not game data):

```bash
dotnet run --project android/tests/RuntimeSmoke -- "$TMPDIR/sotn-smoke" --legacy
dotnet run --project android/tests/RuntimeSmoke --no-build -- "$TMPDIR/sotn-smoke"
```

These test legacy settings conversion and reload and emit the GLES shaders.
`glslangValidator` can validate presentation shaders; its installed version does
not implement `GL_EXT_shader_framebuffer_fetch`, so primitive-shader validation
still needs an Adreno/device run. A syntax-only copy without that extension can
check the remaining shader interface, but is not a substitute for the device.

### Signing and save safety

Read-only inspection found that the installed package is signed by a different
debug certificate from this Mac's `~/.android/debug.keystore`. A build signed by
the Mac key cannot update that installed package in-place. Do not uninstall to
work around it. Recover the original signing key or plan an approved side-by-side
package/save migration first. No install, uninstall, app launch, save copy, or
Thor setting change is part of this sync.

### Build results for this sync

The upstream recompiler and desktop Release build compile successfully.
Android C# compilation succeeds, but the default full-AOT Release build fails
while assembling `SymphonyRecomp.Android.dll`: `temp.s: error: fixup value out
of range` at ARM64 `bl .Lp_*` instructions. This is a native AOT code-size/branch
range failure, not a missing SDK or unresolved C# symbol. The previous hardware
verification above was also JIT-only.

Build a JIT Release package explicitly without changing the project's AOT policy:

```bash
dotnet build android/SymphonyRecomp.Android -c Release \
  -p:RunAOTCompilation=false \
  -p:AndroidSdkDirectory="$ANDROID_HOME" -p:JavaSdkDirectory="$JAVA_HOME"
```

`RunAOTCompilation` is the supported Mono/.NET 10 switch, documented at
https://learn.microsoft.com/en-us/dotnet/android/building-apps/build-properties#runaotcompilation.
Do not interpret a successful package build as a hardware test.
