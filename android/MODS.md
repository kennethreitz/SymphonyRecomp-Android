# Android mods — feasibility spike (2026-11-08)

## Scope and evidence

The real upstream `ModLoader`, `IMod`, events, dependency ordering, source cache,
and folder/ZIP discovery now compile into Android. `ModLoaderStub` is excluded.
The Activity sets the default mod root before generated `Entry.LoadAll()` runs:

```
/sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/mods
```

These are **trusted executable code**, not sandboxed plugins. They have the
app's permissions and can modify game state/files. Use only mods you trust.
Scoped storage may prevent ordinary file managers accessing Android/data; adb
is reliable. APK upgrades must be in-place, with the existing signing key.

- **DLL loading:** optional `"assembly": "MyMod.dll"` in `mod.json`. The managed
  DLL must be relative to its folder/ZIP root. Host API assemblies are shared;
  private managed dependency DLLs can sit beside the entry DLL. Native libraries
  are not installed from mods. Build portable IL against the matching runtime.
- **Roslyn:** the existing 5.6.0 source compiler is included, with its raw-metadata
  fallback for assemblies lacking a file Location. Source compilation remains
  a device feasibility test, not a claimed Android result from a Mac test.
- **JIT/trimming:** Release sets RunAOTCompilation=false, PublishTrimmed=false,
  AndroidLinkMode=None, AndroidUseInterpreter=false. This preserves host/runtime/
  Roslyn/MonoMod reflection targets rather than guessing linker roots. APK size
  grows; no AOT performance claim is made.
- **Contexts/reload:** Android uses non-collectible AssemblyLoadContext instances;
  automatic watching is disabled. Quit normally and start again to apply mods or
  settings. Do not repeatedly toggle/reload code in a running Android process.
- **Native detours: unavailable with the pinned dependency.** RuntimeDetour
  25.3.6 depends on Core 1.3.6 (source commit
  a74a1cf0d15c5e82dfa6ef8bbe7a131b60bed3fd). Its PlatformTriple.CreateCurrentSystem
  explicitly has `OSKind.Android => throw new NotImplementedException()`.
  Linux/macOS Mono ARM64 support is not Android support. Android attribute-hook
  mods are rejected clearly before OnLoad; event/non-detour mods can still run.
  HookManager also refuses manually registered Android detours and reports only
  successfully installed hooks in its global count. A DLL calling MonoMod itself
  is responsible for catching its unsupported-platform error.

Exact upstream evidence:
https://github.com/MonoMod/MonoMod/blob/a74a1cf0d15c5e82dfa6ef8bbe7a131b60bed3fd/src/MonoMod.Core/Platforms/PlatformTriple.cs

Do not spoof Android as Linux: that backend drops its native helper into `/tmp`
and is not an Android/Bionic implementation. Full hook mods need a real Android
MonoMod platform/native-helper port and device validation, or a separate managed
hook-instrumentation design in the recompiler. A package version bump alone is
not an established solution.

## Enabling/disabling

The upstream `defaultEnabled` manifest setting still works. Optionally create
`mods/mod-settings.json` (case-insensitive property names and mod IDs):

```json
{
  "enabled": true,
  "mods": {
    "android-mod-smoke": true,
    "android-source-smoke": true,
    "some-other-mod": false
  }
}
```

`enabled: false` disables all code mods at next startup. Malformed configuration
fails closed and logs the reason. An omitted mod uses the existing saved
`mods.ID.enabled` value, then its manifest's default. This does not add an ImGui
UI to Android or overwrite existing internal settings. C# translation extensions
also call the real loader again; native hook attributes remain unsupported.

## Build

From the existing checkout on the Mac:

```sh
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:/opt/homebrew/bin:$PATH"
export ANDROID_HOME="$HOME/android-sdk"
export JAVA_HOME=/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home
dotnet build android/SymphonyRecomp.Android -c Release \
  -p:RunAnalyzers=false -p:AndroidSdkDirectory="$ANDROID_HOME" \
  -p:JavaSdkDirectory="$JAVA_HOME"
./android/mods/build-smoke.sh
```

APK: `android/SymphonyRecomp.Android/bin/Release/net10.0-android/com.blacklabelhq.symphonyrecomp-Signed.apk`

Deployable fixtures:
- `android/mods/dist/android-mod-smoke/` — precompiled DLL, benign log/event
  listener and a caught native-detour probe. Does not change gameplay or saves.
- `android/mods/dist/android-source-smoke/` — source-only Roslyn/event probe.

The smoke DLL is compiled for portable net10.0 against the desktop target of the
same API, not AOT, and does not ship a duplicate runtime assembly.

## Deployment and proof

Only do this while the Thor is idle and SotN is not foreground. Never uninstall,
clear app data, force-stop, or replace the app with a differently signed build.
The baseline signer SHA-256 is:
`e35962d0b9609c198ec27b54a84e8e8d8cf8cde215c680ac267477051f259753`.

```sh
adb mdns services
adb devices -l
adb shell dumpsys activity activities | rg 'mResumedActivity|topResumedActivity'
"$ANDROID_HOME/build-tools/36.0.0/apksigner" verify --print-certs \
  android/SymphonyRecomp.Android/bin/Release/net10.0-android/com.blacklabelhq.symphonyrecomp-Signed.apk
# STOP if a game is foreground or the signer differs.
adb install -r android/SymphonyRecomp.Android/bin/Release/net10.0-android/com.blacklabelhq.symphonyrecomp-Signed.apk
adb shell mkdir -p /sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/mods
adb push android/mods/dist/android-mod-smoke \
  /sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/mods/
adb push android/mods/dist/android-source-smoke \
  /sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/mods/
# Recheck foreground before launching; do not interrupt another game.
adb shell dumpsys activity activities | rg 'mResumedActivity|topResumedActivity'
adb shell am start -n com.blacklabelhq.symphonyrecomp/crc64ccfb01a6ef77ec7c.MainActivity
# Read existing logs; do not clear the device's logcat buffer.
adb logcat -d -t 4000 | rg 'AndroidModSmoke|AndroidSourceSmoke|\[Mods\]'
```

Look for fresh-launch lines (not stale logs from a prior process):
- `[AndroidModSmoke] DLL_LOADED AndroidModSmoke v1` — DLL instantiated.
- `[AndroidModSmoke] EVENT_OK VSyncEvent (no detour required)` — live game event.
- `[AndroidModSmoke] DETOUR_UNAVAILABLE ...` — expected pinned Android limitation,
  NOT success. `DETOUR_OK before=41 after=42` would instead be actual proof that
  the standalone detour callback ran; do not infer success merely from loading.
- `[AndroidSourceSmoke] SOURCE_COMPILED_AND_LOADED`, then `SOURCE_EVENT_OK` —
  Roslyn and its loaded mod actually ran. A compile error is an honest negative.

No visible game change is made. The event logs are one-shot, not per-frame spam.

## Host regression checks

`android/tests/ModLoaderSmoke` exercises the actual shared loader in a headless
Mac/CoreCLR process with test overlay functions. Eight modes test DLL/source
loading, ZIPs, per-mod/global disable, invalid configuration, invalid DLL paths,
and invalid IDs. A separate desktop-only source fixture checks upstream hook
attributes. Passing these is **not** Android/Mono native-detour proof:

```sh
dotnet build android/tests/ModLoaderSmoke -c Release -p:RunAnalyzers=false
for mode in load select disable invalid zip bad-dll bad-id script-disabled; do
  dotnet run --project android/tests/ModLoaderSmoke -c Release --no-build -- "$PWD" "$mode"
done
dotnet run --project android/tests/RuntimeSmoke -c Release -- "$(mktemp -d)" --legacy
```

## Validation status for this spike

- Release Android build: passed, 0 errors; 49,540,344-byte signed JIT APK.
  APK SHA-256: `d150cae399694a33caec5e0ee7207a83c64fbceaa454c335f7b0dd43af8a018b`.
  apksigner confirms the baseline e35962d0... certificate. No AOT .dll.so files.
- Desktop Release build: passed, 0 errors.
- Eight shared-loader modes: passed on Mac/CoreCLR, including actual standalone
  detour callback, desktop attribute hooks, DLL/source/ZIP loading, and safe mode.
- Existing RuntimeSmoke legacy migration and subsequent reload: both passed.
- Thor: adb initially reported a cached transport then `error: closed`; afterward
  mdns services and devices were empty. No install, push, launch, data clear,
  force-stop, or other device write was performed. Android DLL loading, Roslyn,
  event execution and reflection preservation still require the documented device
  run. Mac evidence must not be presented as proof of those Android results.

## On-device follow-up (2026-11-08, ET)

The initial untrimmed build exposed .NET 10's native JNI marshal-registration
failure (`MainActivity.n_onCreate`). Release now explicitly disables
AndroidEnableMarshalMethods; a clean Java intermediate rebuild was necessary
when switching that property. Android Roslyn also needs raw metadata when
Assembly.Location is a nonempty virtual name, not an existing file. The compiler
now handles that case and resolves bundled platform assemblies by assembly name.

Thor logcat now proves:
- 1:07:13.974 PM ET: `DLL_LOADED AndroidModSmoke v1`
- 1:07:15.279 PM ET: `SOURCE_COMPILED_AND_LOADED`
- 1:07:15.967 PM ET: `EVENT_OK VSyncEvent (no detour required)`
- 1:07:15.968 PM ET: `SOURCE_EVENT_OK`
- `DETOUR_UNAVAILABLE System.NotImplementedException` from
  PlatformTriple.CreateCurrentSystem, confirming the pinned Android limitation.

Blackout revision 00aeb9e was fetched from BlackLabelHQ/BlackoutMod and deployed
as its original `mod.json` plus `source/Blackout.cs` folder, matching the upstream
release workflow's filesystem mod bundling rather than embedding source in the
application. It does not run unchanged: Runtime.View/SaveView were removed by the
upstream settings rewrite, and it also has native PreHooks on dra.EntityBatEcho
and dra.EntityBatFireball. The event handlers darken stage/entity palettes; those
two hooks provide echo/fireball lighting. The mod additionally grants bat/echo/
visibility relics and 200 maximum MP by default. Do not silently strip those
hooks and call it equivalent. A full port needs settings migration and a real
Android hook backend (or an explicit event-based redesign). It remains disabled
in the device's mod-settings.json after this compatibility test. No save was
loaded or modified by the mod.

## Writable asset packs

Android's upstream asset manager now uses:
`/sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/packs`.
The Android render loop pumps pending reloads, allowing directory/ZIP changes
to reload without a native mod hook. This is independent of code-mod enabling.

A pack is a folder or ZIP with `pack.json` at its root. Minimal manifest:

```json
{"formatVersion":1,"id":"my-pack","name":"My pack","version":"1.0","priority":100}
```

RGBA textures go under `textures/INDEXHASH.png`, or
`textures/INDEXHASH_CLUTHASH.png` to match one palette. Each hash is exactly 16
hex digits. `.idx.png` is for indexed-mode art. Explicit manifest `textures`,
`cluts`, `sfx` and `xa` entries are also supported; see upstream PackManifest.cs
and the desktop Asset Pack Editor. Optional root `packs.json` uses `order` and
`disabled` arrays to select priority and turn packs off. Pack ZIPs are not code
mods; put them in packs, not mods.

Run `python3 android/packs/make-test-pack.py` to generate the original neon
checker marker under `android/packs/dist/android-title-marker`. Its target is one
observed title/menu texture/palette; it contains no extracted game pixels. Upload
its texture first and pack.json last, or upload a complete ZIP, so discovery
cannot see half-written content. Do not redistribute locally dumped game art.

Public reference links:
- https://github.com/BlackLabelHQ/SymphonyRecomp/releases
- https://github.com/BlackLabelHQ/RecompOne/blob/master/RecompOne.Runtime/Assets/PackManifest.cs
- https://github.com/BlackLabelHQ/RecompOne/blob/master/RecompOne.Runtime/Host/Window/Tools/AssetPackEditor.cs

Two public searches found no verified SymphonyRecomp community asset-pack
release to recommend. Unrelated emulator or Banjo packs are not presumed
compatible; no unknown pack downloads were made.

Asset replacement proof: at 1:10:47.300 PM ET, logcat reported
`android-title-marker: loaded texture ...ac6d04dd968b34ca_58ea135bbf174f98.png 255x144`.
The original green/magenta/cyan checker is visibly present behind the title logo
in `../screens/sotn-title-marker-pack.png` (outside this source checkout). The
first page-fallback probe loaded but was not useful visual proof; the second
probe matches an actual visible source tile. Both test packs are disabled in
packs.json after the demonstration, with files retained. The smoke code mods
and incompatible Blackout are likewise disabled after proof, not deleted.

The Thor routes a default HOME key to the focused secondary display. Use
`adb shell input -d 0 keyevent HOME` for the primary game display. This device
may show the Android launcher chooser because its primary default Home is unset;
no default or other-app settings were changed.
