# Phase 0 implementation decisions

## Included

- Canonical `Game` model separated from `GameInstallation` so one game can later have multiple store/library identities.
- `PlaySession` and `ProcessRule` are modeled now and have SQLite tables, but Phase 1 will implement actual process detection and session tracking.
- `IGameSourceAdapter` is an internal interface. A dynamic plugin loader is intentionally deferred.
- SQLite is accessed directly with `Microsoft.Data.Sqlite`.

## Deferred to Phase 1+

- Steam library discovery
- Manual game add/edit
- Launch execution
- Process detection
- Playtime tracking logic
- Cover art and metadata
- Duplicate reconciliation across stores
- Background companion apps
- Gameplay overlay

This keeps Phase 0 limited to infrastructure that later phases actually need.
