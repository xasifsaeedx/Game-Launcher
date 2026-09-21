# Game Launcher

A lightweight, open-source Windows game launcher built around a local unified library. Current implementation: **Phase 7 - History + Console Accounts**.

## Current features

- Installed-game discovery for Steam, Epic, GOG, EA app, Ubisoft Connect, Battle.net, and Xbox / Microsoft Store `XboxGames` installs.
- Manual game entries.
- Conservative cross-store duplicate reconciliation.
- Automatic/local cover-art caching.
- Game launching with process detection and persisted playtime.
- Per-game smart launch profiles with pre-launch, companion, and post-game programs.
- Gameplay-only RTSS overlay with FPS, GPU usage, and GPU temperature.
- Hatchable synchronization for Next 100 ranking, status, progress, rating, playtime, and last-played data.
- Hardware-aware Graphics Optimizer with safe backup/apply/restore for verified game configs.
- **Gaming History**:
  - live Steam owned-game/history sync through Valve's Web API
  - encrypted Steam Web API key storage using Windows DPAPI
  - direct PlayStation Data Access `.xlsx` import
  - flexible CSV/JSON imports for PlayStation, Xbox/Microsoft, GOG, EA, Ubisoft, Battle.net, Epic, and Steam
  - historical account snapshots remain separate from installation state
  - imported account playtime remains separate from launcher-tracked sessions to avoid double counting
  - idempotent source snapshots keep the greatest known cumulative playtime and latest known last-played timestamp
  - Gaming History view merges matching titles conservatively while retaining source records

## Gaming History setup

### Steam

1. Open **History** > **Steam setup**.
2. Enter your numeric SteamID64.
3. Create/copy a Steam Web API key from Valve's API-key page.
4. Choose whether Steam history should refresh when the launcher starts.
5. Click **Save & test**.

The Steam API key is encrypted locally for the current Windows user. The launcher never asks for your Steam password.

Steam's `GetOwnedGames` API can return owned games, names, and account playtime when the account's game details are visible.

### PlayStation

Sony's current Data Access workflow provides an Excel file.

1. In PlayStation Account Management, request a Data Access download.
2. Download the resulting `.xlsx` file when Sony makes it available.
3. In **History**, choose **PlayStation** and **Import account file...**.

The parser only imports worksheets that look like game/title/play history and contain recognizable game-title columns. If Sony changes the workbook or the export does not contain game-history rows, the launcher reports that instead of guessing.

### Other account/store files

For Xbox/Microsoft, GOG, EA, Ubisoft, Battle.net, Epic, or fallback Steam imports, Phase 7 accepts CSV or JSON files with flexible common field names.

Recognized fields include:

- title / game title / game name / product name
- game ID / AppID / product ID / title ID
- platform / system / device
- playtime in seconds, minutes, hours, or duration text
- last played date/time

If the source export does not use a supported machine-readable format, convert only the relevant game rows to CSV/JSON rather than giving the launcher account passwords or session cookies.

## History data rules

- Account history is not represented as fake local installations.
- Imported cumulative playtime is not converted into launcher play sessions.
- Launcher-tracked playtime is shown separately.
- Re-importing the same source/external ID updates the snapshot instead of duplicating it.
- A later import cannot reduce previously known cumulative account playtime.
- A later import cannot move the known last-played timestamp backward.
- Cross-source title merging uses conservative normalized-title matching only.

## Graphics Optimizer safety

Automatic config editing remains intentionally narrow and reversible. Unsupported games receive recommendations only.

## Gameplay overlay

RTSS must be installed for the in-game OSD. MSI Afterburner is not required.

## Stack

- C# / .NET 10 LTS
- WPF
- SQLite via `Microsoft.Data.Sqlite`
- ClosedXML 0.105.1 for PlayStation Excel imports
- LibreHardwareMonitorLib 0.9.6
- RTSS `RTSSSharedMemoryV2`
- Windows DPAPI via `System.Security.Cryptography.ProtectedData`
- Hatchable companion web application/API

## Build

Requirements:

- Windows 10/11
- .NET 10 SDK
- RTSS installed if you want the gameplay overlay

```powershell
.\build.ps1
```

Run:

```powershell
.\run.ps1
```

Local launcher data:

```text
%LocalAppData%\MyGameLauncher\
```

## Project plan

- Phase 0: Foundation - complete
- Phase 1: Core MVP - complete
- Phase 2: Unified Library - complete
- Phase 3: Smart Launching - complete
- Phase 4: Gameplay Overlay - complete
- Phase 5: Hatchable Sync - complete
- Phase 6: Graphics Optimizer - complete
- **Phase 7: History + Console Accounts - current**
- Phase 8: Final Product

See `PHASE7_NOTES.md` for implementation boundaries.

## License

MIT. See `LICENSE`.
