# Skip Prologue

An event-only source mod for the US Android port. Default disabled. Enable
`"skip-prologue": true` in the existing `files/mods/mod-settings.json`, preserving
other settings. The APK seeds missing source files; it never overwrites edited
source or enables a mod by itself. Restart the app after changing the toggle.

## Scope and safety fence

Only a newly confirmed ALUCARD game is eligible. The mod must first observe the
native fresh-game name-entry state (MainMenu / engine 0x11), then its confirmed
fade-out state (engine 0x12), fresh-game flag zero, ST0 prologue stage, Alucard
character and no demo playback. A stage id or a zero fresh-game flag alone is
insufficient. Cancelling name entry, choosing a different character/custom start,
or receiving SaveLoadedEvent cancels the fence. Enabling this mod in an existing
running game does nothing. It never opens, reads, writes or loads save files,
and it never calls StoreSaveData or ApplySaveData. No native detours or executable
code patches are installed.

## Native flow, not hardcoded final stats

1. The confirmed fresh start selects NO3 (0x41), the game's first-visit castle
   entrance, rather than ST0 (0x1f). Randomizer implementations use this same
   stage routing, but their executable/reward patches are not copied here.
2. The opening movie/title-card phase exits through native `func_800E4970`, the
   exact cleanup/loader routine used by HandleVideoPlayback's START-skip path.
   The narration, Richter staircase/boss battle and burning-photo/interlude are
   bypassed because ST0 is never loaded or played.
3. Immediately before native InitStatsAndGear, the mod supplies a documented
   synthetic Richter result: 50/50 HP, no Maria rescue, Cross, 30 hearts both at
   the battle's beginning and end, and a one-second completion. It clears the
   fresh run's 32 time-attack records and visited masks, then calls the native
   TimeAttackController setter for Dracula. The native initializer calculates
   Alucard's stats, bonuses, starting gear and item rewards itself. There are no
   writes to final STR/CON/INT/LCK, MP, level or equipment values.
4. After the native fresh-game castle-flag reset, before room initialization, it
   selects the existing completed-entrance spawn selector 0x11 and sets only
   g_CastleFlags[PROLOGUE_COMPLETE]. These are the values used by the native
   already-complete NO3 entry route. This avoids the outdoor automatic run and
   entrance-door forced walk without bypassing normal player/room initialization.
5. Once normal Alucard gameplay begins, the one-shot operation is complete and
   performs no further game writes. A subsequent fresh name-entry flow can arm
   a new operation. Castle Gear's generic MODS card reports the phase.

The canonical outcome uses the native fastest, unassisted, full-health tier and
Cross bonus, following the practice-script convention of preserving Cross's MP
bonus. It deliberately does not pretend the unplayed fight had a player-selected
performance. The synthetic Dracula time-attack entry is 00:00:01; all main-game
boss and Maria records start unset. Nonzero 30 hearts and fewer than 41 starting
hearts mean no zero-heart Heart Refresh or 41-heart Neutron Bomb reward. Native
name-based modes/starting-equipment rules remain the native initializer's job;
separate Richter/Maria character modes and custom starting stages are untouched.

This is not Death Skip: the later encounter that removes Alucard's equipment,
Maria conversations, boss progression and other gameplay remain normal. It does
not mark Maria as already met, grant equipment, or unlock the inverted castle.
The existing practice Lua script's broad character polling and Maria-record write
are intentionally NOT reproduced, because they are not appropriate for save-safe,
new-Alucard-only behavior.

## References checked

- Native fresh-name confirmation, stage selection and opening transition:
  https://github.com/Xeeynamo/sotn-decomp/blob/master/src/st/sel/select.c
- HandleNowLoading, HandlePlay reset, HandleVideoPlayback's skip route and the
  completed-entrance selector/flag:
  https://github.com/Xeeynamo/sotn-decomp/blob/master/src/dra/game_handlers.c
- Native PROLOGUE_COMPLETE behavior in EntityCastleDoorTransition:
  https://github.com/Xeeynamo/sotn-decomp/blob/master/src/st/no3/e_outdoor_ents.c
- US PlayerStatus offsets, relic/character/stage/time-attack layouts:
  https://github.com/Xeeynamo/sotn-decomp/blob/master/include/game.h
  https://github.com/Xeeynamo/sotn-decomp/blob/master/include/castle_flags.h
- Randomizer `applynoprologuePatches`, including its warning-worthy reset of
  time-attack records so bosses still spawn:
  https://github.com/3snowp7im/SotN-Randomizer/blob/master/src/util.js
- Practice-script Cross-bonus precedent and Maria-record caveat:
  https://github.com/Nijoja/Sotn-Projects/blob/main/NoPrologue.lua
- Native prologue reward conditions explained for runners:
  https://castlevaniaspeedruns.com/SOTN/Alucard_Any%25_NSC#Prologue
- This port's existing native initializer adapter, not copied into the mod:
  `patches/rando/RandoPatch.cs`, `InitStatsAndGear`, with the US generated call
  sites checked locally. Addresses are layout facts for this version only.

No decompilation implementation, ROM bytes or extracted artwork is included.

## Testing without saving

Run `dotnet run --project android/tests/CastleGearSmoke -c Release` for ROM-free
fence/profile tests. On device, start a NEW game or choose a visibly EMPTY slot;
never select an existing save and never visit/activate a save point. Capture the
entrance and native status menu, check the SkipPrologue COMPLETE log and zero
hook count, then leave the test game unsaved. Turning the mod off restores the
normal prologue for future fresh starts; it cannot undo a skip already completed
in an unsaved running session.


## Thor validation (November 8, 2026)

A visibly empty left-card slot was selected, confirmed with the native default
name, and left unsaved. The event trace observed name-entry engine 17 then
confirmation engine 18 (decimal, 0x11 then 0x12), Game_VideoPlayback, loader step
10, post-reset Play_Init and normal engine gameplay in NO3. ST0 was not loaded.
Both source mods compiled on device; total native hooks remained zero.

The native pause menu and runtime trace agreed: level 1, HP 80/80, MP 30/30,
hearts 10/60, base STR 13, CON 12, INT 12, LCK 12, EXP 0, NEXT 100, gold 0,
one explored room, zero kills. Native starting equipment includes Alucard Sword,
Alucard Shield, Dragon Helm, Alucard Mail, Twilight Cloak and Necklace of J.
Gear showed the same vitals/equipment and Skip Prologue: Complete, with zero save
snapshots this session. No save-point operation was performed. Later Death/Maria
encounters were not played through in this short smoke test; their flags and
scripts are not bypassed by the mod.

Proof captures are outside the source tree under `~/repos/sotn-android/screens/`:
`skip-prologue-entrance.png`, `skip-prologue-native-status.png`,
`castle-gear-skip-prologue-status.png`, `castle-gear-skip-prologue-complete.png`,
and `castle-gear-skip-prologue-map.png`.
