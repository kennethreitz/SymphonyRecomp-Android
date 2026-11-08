# Castle Gear

A read-only, gothic second-screen companion for the AYN Thor. Its card structure
is inspired by the Kanto Gear companion in the local gen1recomp project, with
original Android Canvas chrome rather than borrowed game artwork.

- MAP: native game map, explored colors, dim unexplored blueprint, player marker.
- STATUS: HP/MP/hearts, level, experience, gold, equipment and relic count.
- JOURNAL: play time, room count, time-attack boss records and music track id.
- Touch tabs to switch. MAP supports pinch zoom, drag to pan, +/- and FIT reset.
- Map percentage and named music archive remain planned.

## Alignment and data

US-disc layout documented by Xeeynamo/sotn-decomp, include/game.h and
src/dra/5087C.c and 624DC.c. The decoder is original code using layout facts.

- RAM blueprint: 0x801E0000, 256x256 pixels, 4bpp, low nibble first, 128 bytes/row.
- Explored flags: 0x8006BB74, two 0x400-byte castle banks, four 2-bit cells/byte,
  high pair first. Low bit is explored; high bit is bought/known map.
- Cells have a four-pixel pitch, not the old five-pixel tracker-art pitch.
- Live GPU map: VRAM (832,256), 64 words x 256 rows, four pixels/word.
- Actual map CLUT: PSX id 0x1c0, VRAM (0,7), 16 BGR555 colors.
- HLE VRAM is authoritative. Read through GpuJobs and Backend.ReadVram rather
  than the CPU shadow, which can be stale until a StoreImage readback.
- Inverted castle rotates the complete bitmap and marker through one transform.
  There are no art-specific row offsets or separate rectangular room fills.
- Player cell: room origin at 0x800730B0/B4 plus signed player offset at
  0x800973F0/F4 shifted by eight.
- g_Status is 0x80097964. Actual seven equipment ids start at +0x29c;
  attackHands at +0x2b8 contains damage values and must not be used as item ids.

Game-thread VSync snapshots are published to the UI as immutable frames at 10Hz.
No companion action writes game memory, memory cards or save files. The view
waits instead of exposing stale stage data in menus or during stage loads.

Run ROM-free layout, transform, viewport and playback-key tests:

```sh
dotnet run --project android/tests/CastleGearSmoke
```

Normal castle is device-tested; inverted transformation is synthetic-test-covered
and still needs a real inverted-castle save for visual confirmation.
