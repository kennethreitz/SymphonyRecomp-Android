# Castle Gear

A read-only, gothic second-screen companion for the AYN Thor. Its card structure
is inspired by the Kanto Gear companion in the local gen1recomp project, with
original Android Canvas chrome rather than borrowed game artwork.

- STATUS opens first: HP/MP/hearts, level, experience, gold, equipment and relic count.
- MAP: native explored map and player marker. Unexplored blueprint is hidden.
- JOURNAL: play time, room count, time-attack boss records and music track id.
- Touch tabs to switch. Opening MAP starts at 4x centered on the current room.
  Pinch zoom, drag to pan, +/- and FIT reset remain available.
- Map percentage and named music archive remain planned.

## Alignment and data

US-disc layout documented by Xeeynamo/sotn-decomp, include/game.h and
src/dra/5087C.c and 624DC.c. The decoder is original code using layout facts.

- Explored flags: 0x8006BB74, two 0x400-byte castle banks, four 2-bit cells/byte,
  high pair first. Low bit is explored; high bit is bought/known map.
- Cells have a four-pixel pitch, not the old five-pixel tracker-art pitch.
- Live GPU map: VRAM (832,256), 64 words x 256 rows, four pixels/word.
- Actual map CLUT: read g_ClutIds[0x1c0] at RAM 0x8003C484. Decode its
  VRAM origin from the live PSX CLUT id. InitFade stores logical palette index
  0x1c0 in Primitive.clut; it is not the packed PSX CLUT id 0x1c0.
- GLES readback uses guaranteed RGBA/UNSIGNED_BYTE and converts to BGR555.
  Packed RGB5_A1 ReadPixels is not guaranteed and failed on the Thor.
- Explored counts include visited cells only. Charted-only counts include bought
  map cells that have not been visited, so the numbers do not double-count.
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

Run ROM-free layout, transform, viewport and GLES color-conversion tests:

```sh
dotnet run --project android/tests/CastleGearSmoke
```

Normal castle is device-tested; inverted transformation is synthetic-test-covered
and still needs a real inverted-castle save for visual confirmation.
