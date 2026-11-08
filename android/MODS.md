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
adb shell am start -a android.intent.action.MAIN \
  -c android.intent.category.LAUNCHER -p com.blacklabelhq.symphonyrecomp
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
