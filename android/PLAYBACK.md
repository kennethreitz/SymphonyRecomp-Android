# Playback controls on Android

- Hold R3: momentary fast-forward, targeting 3x (180 virtual VBlanks/second).
- Hold R3 for three seconds: latch fast-forward. Release leaves it locked.
- Press R3 again: unlock and return to normal speed immediately.
- L3: rewind is unavailable in this runtime. An explicit << UNAVAILABLE indicator
  is shown; this does not pretend to rewind or modify player position.
- Both clicks are intercepted before PSX pad mapping. No default SotN gameplay
  actions in the port bind to L3/R3.
- The upper-right game badge and Castle Gear header distinguish >> FF 3x from
  >> LOCKED, and display hold progress toward the three-second threshold.
- Leaving the activity resets holds and latch, preventing stuck fast-forward.
- Audio is muted during fast-forward without changing saved volume preferences.
- Actual acceleration is device/performance-dependent, not a guaranteed 3x.

## Existing runtime plumbing reused

The runtime already provides Interrupts.Turbo, ForceVBlank, FrameClock, a
LibEtc.WaitVBlanks fast path and presentation pacing. The Android control reuses
ForceVBlank and that fast path, adding a wall-clock 180Hz cap instead of invoking
unbounded turbo. Presentation pacing is bypassed while active; interpolation
continues catching up to the newest game frames.

## Why rewind is not implemented

A RAM ring buffer is not a complete state snapshot. The recompiled game executes
as nested managed C# calls, not a single resumable emulated-PC interpreter loop.
CpuContext.Snapshot restores registers for IRQ handling; it cannot restore a past
managed call continuation. An arbitrary old VSync could have a different live
managed stack even if RAM and registers were copied back.

Other state outside RAM also lacks a complete capture/restore interface:
GPU VRAM and interpolation graphs, active overlay dispatch registrations, BIOS
queues, hardware timers/IRQs, SPU voices and reverb, XA/CD streams and asynchronous
memory-card operations. Restoring only some of these risks crashes or writes
against an inconsistent memory-card state.

A safe rewind implementation needs explicit resumable checkpoints, complete
machine/host state serialization, paused audio/GPU synchronization, and a policy
that forbids rewinding persistent card writes. Only then should a memory-capped
20-30 second ring be added. No unsafe partial rewind buffer is allocated now.
