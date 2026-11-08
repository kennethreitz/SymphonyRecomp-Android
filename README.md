# SymphonyRecomp Android

A hard fork of [BlackLabelHQ/SymphonyRecomp](https://github.com/BlackLabelHQ/SymphonyRecomp) for Android, focused on the AYN Thor. This fork carries Android patches and merges upstream improvements. It is not affiliated with or endorsed by BlackLabelHQ.

Please report Android-fork issues here, not to upstream or the SOTN Decomp Discord. SymphonyRecomp and the SotN decompilation are separate projects.

## Current state

- ARM64 Android head with OpenGL ES 3.2 rendering and Android audio/input.
- Upstream 1.0 prerelease work, widescreen fixes, room extensions, translations and randomizer improvements.
- Castle Gear: touch-operated Status, Map and Journal pages on the Thor's second screen. Map uses the live game bitmap, explored colors and player position, with 4x player-centered opening, pinch/drag and FIT controls.
- Writable external mods and asset packs. Precompiled DLL and Roslyn source event/API mods were smoke-tested.
- In-place APK updates preserve app data when signed with the installed key.

### Limits

- Mono/Android JIT only. Full AOT is not supported by this mod-enabled build.
- MonoMod native detours are not supported on Mono ARM64 Android. Hook-dependent mods, including upstream blackout, do not work.
- Shaders/post-processing are off on this Android build.
- No fast-forward, rewind or L3/R3 playback controls. The attempted implementation was reverted.
- Inverted-map transforms have synthetic coverage but still need a real inverted-castle save for device verification.
- Map percentage and named music archive are not implemented. Journal boss counts are time-attack records, not a complete boss checklist.

## Build and install

Use a legally owned North American PlayStation disc. No disc image is provided. Follow [android/README.md](android/README.md) to generate the game code locally and build the Release JIT APK with .NET 10 and the Android workload.

```sh
dotnet build android/SymphonyRecomp.Android -c Release -t:SignAndroidPackage
dotnet run --project android/tests/CastleGearSmoke
```

Android package: `com.blacklabelhq.symphonyrecomp`. Update with `adb install -r` and the same signing key. Do not uninstall or clear data to update.

See [Castle Gear](android/CASTLE-GEAR.md) and [Android mod support](android/MODS.md) for data layout, controls, mod examples and external paths. Mods and packs live under `/sdcard/Android/data/com.blacklabelhq.symphonyrecomp/files/mods` and `files/packs`.

## Private snapshot and redistribution

This repository and its release are private personal development snapshots, not approved public distributions. The APK embeds locally generated disc-derived game code. Tracked upstream artwork also needs a provenance/license audit. No claim is made that this snapshot contains no game-derived material. Do not make the repository or APK public.

A clean distributable would need to generate or load game code from each user's own disc at runtime, and extract or replace artwork whose redistribution rights are not established. Owning a disc or keeping a repository private does not grant general redistribution rights.

The upstream superproject currently has no top-level license file; this fork does not invent a license for it. Preserve all existing notices. RecompOne retains its [MIT license](RecompOne/LICENSE), which does not license game-derived code or assets.

## Credits

- [BlackLabelHQ and SymphonyRecomp contributors](https://github.com/BlackLabelHQ/SymphonyRecomp) for the original recompilation project and upstream patches.
- [RecompOne contributors](https://github.com/BlackLabelHQ/RecompOne) for the static recompiler and runtime.
- [Xeeynamo and sotn-decomp contributors](https://github.com/Xeeynamo/sotn-decomp) for documenting the game's memory layouts.
- Kenneth Reitz for the Android/AYN Thor port. Castle Gear's card-based companion structure is inspired by Kanto Gear in gen1recomp.

Castlevania and Symphony of the Night belong to their respective rights holders. This fork is unaffiliated with them.
