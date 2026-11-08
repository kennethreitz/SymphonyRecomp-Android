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


## Castle Gear provider API v1

`SymphonyRecomp.Android.SecondScreen.GearProviders` is a small public, generic,
push-only API in the Android app assembly. Source mods already receive that
assembly's metadata from the normal Roslyn loader. No Android UI types, native
hooks, or detours are needed. `ApiVersion` is currently `1`.

```csharp
using SymphonyRecomp.Android.SecondScreen;
GearProviders.Registration provider = GearProviders.Register("my-mod");
provider.Publish(new GearCard("MY CARD", new[] {
    new GearField("Named field", "Current value"),
    new GearField("Another field", "42")
}));
// Publish replaces all this provider's cards atomically; Publish() clears them.
// In IMod.OnUnload, remove your event listeners and dispose your registration.
provider.Dispose();
```

Collect data on the game thread, for example from `VSyncEvent`, and publish
strings at a modest rate. Never read mutable game memory from a UI callback.
The app copies collections and strings on publication; snapshots are read-only.
No provider callbacks execute during drawing. Disposing removes the provider's
cards and rejects further publication. IDs are case-insensitively unique.
Registration is bounded to 16 providers, each publication to 8 cards, each card to
64 fields. Titles, field names, and values are limited to 48/40/96 characters;
control characters are stripped. The MODS tab displays nine rows per card page,
with PREVIOUS/NEXT buttons. Long rows are ellipsized, not drawn over neighbors.
Empty providers are hidden; card order is provider ID followed by publish order.
UI state is not a new footer: navigation stays inside the content panel.

### CastleGearPlus source mod

`android/mods/CastleGearPlus/` contains only source and a manifest. It subscribes
to existing `VSyncEvent`, `PlayerLoadedEvent`, and `SaveCreatedEvent`, and publishes
four named cards through the same public API. It never writes game memory,
loads or saves a game, installs detours, or accesses memory-card files.

Enable it explicitly (default is disabled) in the device's existing
`files/mods/mod-settings.json`, preserving all other entries:

```json
{ "enabled": true, "mods": { "castle-gear-plus": true } }
```

The APK bundles this small source mod and seeds missing files using the app's
own `GetExternalFilesDir`-based storage on startup. Existing source files and
mod settings are never overwritten by the seeder. This avoids depending on adb
creating new directories inside scoped Android/data storage. Android documents
this supported app-owned storage at
https://developer.android.com/training/data-storage/app-specific.

Alternatively deploy the folder to
`/sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/mods/`. Install a
matching-signer build in place with `adb install -r` when approved, and launch
the app. Do not uninstall or clear data. Log proof is
`[CastleGearPlus] SOURCE_LOADED API=1 EVENTS_ONLY NO_DETOURS`, followed by a
`[CastleGearPlus] LIVE ...` line once the owner enters gameplay. Open MODS on
Castle Gear. No generated or extracted game artwork/data is part of this mod.

Reliable fields and precise meanings:

- Map completion uses the native US save-selector equation, integer tenths:
  `floor(rooms * 1000 / 942) / 10`, displayed with one decimal. It is not clamped
  at 100%; both castles can reach 200.6%. Rooms use native `g_RoomCount`.
- Native total kills are `g_Status.killCount`. Session kills are observed
  positive increments, not a count of enemy sprites disappearing. A saturated
  native kill counter cannot expose further increments.
- Observed HP loss is the sum of downward HP deltas between consecutive active
  native play-clock frames in the same room/area with unchanged maximum HP.
  It includes environmental/poison loss, excludes healing, and is NOT raw damage
  taken, attacker attribution, damage dealt, or DPS. The last observed HP loss is
  also shown. Skipped frames, menus, stage transitions, loads and new-player
  events break comparison baselines; simultaneous healing can hide loss.
- Game overs are observed transitions into the native Game_GameOver state after
  live castle gameplay, counted once, not inferred from low HP.
- Save snapshots count existing `SaveCreatedEvent` notifications after
  `StoreSaveData_dra`. This proves native snapshot serialization, NOT successful
  memory-card/disk completion. The mod itself never triggers a save.
- Relic checklist uses the FOUND bit, not the ACTIVE bit. The US game has 28
  obtainable relics; JP-only slots 23 and 24 are excluded. New relic types this
  session count newly observed FOUND transitions; loading a save cannot count as
  collecting relics.
- Location shows the existing stage name/ID and the room's tilemap origin, not
  an invented globally unique room ID. Per-area/session time counts consecutive
  active native play-clock frames at 60 Hz, excluding pauses, loading, movies,
  title screens and the Richter prologue. Time accumulated before a mod/app
  restart is not persisted. The current card freezes its last snapshot while
  gameplay is unavailable.

A session is this mod instance's lifetime. Loading another save breaks baselines
but retains accumulated session totals; restarting the app resets them. Publish
runs once per second; sampling runs on VSync with native-clock deduplication.

Intentionally omitted: ordinary item pickups (inventory gains can also be
purchases, equipment changes or scripts), bestiary/encounter counts, damage
attributed to the player, DPS, attacker/last-hit identity and confirmed successful
saves. The current event surface does not identify these reliably. A zero
session counter is not proof that its increment path was exercised on device.

Memory-layout facts and native percentage equation were checked against:
- https://github.com/Xeeynamo/sotn-decomp/blob/master/include/game.h
- https://github.com/Xeeynamo/sotn-decomp/blob/master/src/st/sel/select.c
  (`percHi = (percHi * 1000) / 942`, both selector presentations).
- The port's existing `events/GameLifecycleEvents.cs` and generated US call
  instrumentation for `StoreSaveData_dra` / `ApplySaveData_dra`.

No implementation from those decompilation sources was copied into this mod.
ROM-free coverage in `CastleGearSmoke` exercises the provider copy/bounds/unload
contract, native layout sampling, percentage rounding, relic availability,
active-time attribution, counter deltas, pause/load/room/max-HP exclusions and
single game-over/save-snapshot counting. Device proof should validate source
compilation, event dispatch, live values and card paging without causing a
player death or writing any save slot.


### Thor validation, November 8, 2026

- 112 ROM-free checks passed, including bundled source preservation.
- Matching certificate `e35962d0b9609c198ec27b54a84e8e8d8cf8cde215c680ac267477051f259753`;
  in-place installation succeeded at 2:07 PM EST. No data was cleared.
- Fresh process 32755 compiled the source mod on device, registered the API,
  and logged one loaded mod with zero registered hooks and zero hooked functions.
- First live sample: 57 rooms, 6.0%, 86 native kills. During the owner's gameplay
  the cards updated to 61 rooms, 6.4%, 87 kills and 1 observed session kill.
  Percentages were checked against the native selector equation, not by opening
  or modifying a save. The user's gameplay, not automation, provided these gains.
- Location showed Alchemy Laboratory, area 0x0C, room origin 9,27. Active area
  time visibly advanced from 00:00:47 to 00:00:48. The 28-relic checklist showed
  Cube of Zoe HAVE and other available relics MISSING, and all seven card pages
  were navigable without affecting the primary game display.
- HP loss, game-over, save-snapshot and new-relic increment paths are covered
  with synthetic events/samples. No death, save write or pickup was induced by
  automation solely to exercise them on the device.
- Proof images on the development Mac, `~/repos/sotn-android/screens/`:
  `castle-gear-plus-summary.png`, `castle-gear-plus-location.png`,
  `castle-gear-plus-relics-have.png`, `castle-gear-plus-relics-missing.png`,
  `castle-gear-plus-areas.png`, and `castle-gear-plus-game.png`.
- Direct adb folder creation initially failed under scoped storage. Source now
  deploys through app-owned storage as documented above; no root, permission
  escalation or additional storage permission is used. The existing settings
  file was updated normally, preserving the disabled smoke and Blackout mods.

Run the complete ROM-free companion checks from the checkout root:

```sh
dotnet run --project android/tests/CastleGearSmoke -c Release
```

Repeated VSync notifications within one native clock frame retain the original
comparison baseline. Title/demo transitions cannot count as player game overs.
Bundled source is intentionally seed-only: deploy an updated source file normally
into the existing mod folder when iterating; user source is never silently replaced.
