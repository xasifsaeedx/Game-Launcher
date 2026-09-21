# Phase 1 - Core MVP

Implemented from the project plan:

- Manual game add with executable, optional cover image, and optional launch arguments.
- Local Steam discovery from registry, `libraryfolders.vdf`, and `appmanifest_*.acf` files.
- Stable Steam game/install IDs so repeated scans do not duplicate records.
- Launch through an executable or Steam `steam://rungameid/...` URI.
- Process detection for Steam games by finding newly started processes inside the game's install folder.
- Persisted play sessions and total tracked playtime.
- Basic WPF cover-grid library UI with source, playtime, and Play controls.
- Backward-compatible migration of the Phase 0 SQLite database.
- GitHub Actions Windows build/test workflow.

## Deliberately deferred

Phase 1 does not add online metadata/cover-art lookup, cross-store duplicate merging, background companion apps, RTSS overlay, Hatchable sync, or graphics optimization. Those remain in their planned later phases.

## Runtime tracking scope

Playtime is tracked while the launcher is running. Closing the launcher cancels the active tracking operation and records playtime through the cancellation point. Crash/reboot recovery for an already-open session is not part of Phase 1.
