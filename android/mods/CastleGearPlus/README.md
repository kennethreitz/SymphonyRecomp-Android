# Castle Gear Plus

A read-only, event-only source mod for the Android Castle Gear companion.
No detours, extracted assets, memory writes or save-file access.

Copy this folder into the app's `files/mods/` and enable `castle-gear-plus` in
`mod-settings.json`. Use the MODS tab and its PREVIOUS/NEXT card-page buttons.

The provider API, fields, session semantics, limitations, test commands and
memory-layout references are documented in [android/MODS.md](../../MODS.md#castle-gear-provider-api-v1).

Map completion matches the native integer-tenths equation. Kills and HP loss
are observed deltas, not speculative combat instrumentation. Save snapshots are
not confirmed disk saves. Relics exclude the two unavailable US slots.
