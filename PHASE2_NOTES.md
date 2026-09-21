# Phase 2 - Unified Library

## Implemented

- Steam installed-game discovery retained from Phase 1.
- Epic discovery from Epic launcher `.item` manifests.
- GOG discovery from installed-game registry records.
- EA discovery from EA Games registry records.
- Ubisoft Connect discovery from launcher installation registry records.
- Battle.net discovery from Blizzard/Activision installed-program records.
- Xbox / Microsoft Store discovery for modern `XboxGames\<Game>\Content` installations.
- Central source synchronization with per-source failure isolation.
- Conservative duplicate reconciliation based on normalized exact titles.
- Multiple store installations remain available under one unified library item.
- Automatic local artwork detection.
- Automatic Steam library-cover download for Steam games.
- Exact-title Steam Store lookup as a no-key artwork fallback for other PC games.
- Persistent local cover cache.
- Unified-library UI with multi-source labels and installation counts.
- Phase 2 automated tests covering discovery parsers, duplicate reconciliation, artwork caching, persistence, and playtime.

## Intentional limits

- No account sign-in or cloud-library history is used in this phase.
- No fuzzy duplicate auto-merging. Editions/remasters remain separate unless titles normalize identically.
- Protected arbitrary UWP apps under `WindowsApps` are not scraped; Xbox/MS Store support targets the standard Xbox game installation layout.
- Publisher launchers change their local registry/manifests occasionally. Each adapter is isolated so one failing source does not stop the rest of the sync.
- Artwork lookup is best-effort and never blocks library use.

## Deferred

- Game + companion-program launch profiles: Phase 3.
- Gameplay overlay / RTSS integration: Phase 4.
- Hatchable synchronization: Phase 5.
- Automatic graphics settings: Phase 6.
- Historical online account/library imports: Phase 7.
