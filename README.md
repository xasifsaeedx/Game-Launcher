# My Game Launcher

A lightweight, open-source Windows launcher focused on a unified local PC library, game-session tracking, controller use, and personal gaming history.

**Current release target: v1.1.0.**

## Core features

- Detects installed games from Steam, Epic, GOG, EA app, Ubisoft Connect, Battle.net, and Xbox / Microsoft Store `XboxGames`.
- Manual game entries.
- Conservative duplicate reconciliation across stores.
- Automatic/local cover-art caching.
- Launches games and tracks local play sessions/playtime.
- Per-game smart launch profiles with pre-launch, companion, and post-game programs.
- Gameplay-only RTSS overlay for FPS, GPU usage, and GPU temperature.
- Personal Game Library integration from a linked Google Sheet, including Next Play rank/status metadata.
- Persistent local 1–10 game ratings, independent of the linked Sheet.
- Hardware-aware Graphics Optimizer with safe preview/backup/apply/restore on verified configurations.
- Gaming History import from Steam API, PlayStation Data Access Excel, and flexible CSV/JSON account exports.
- Favorites, search, and All / Favorites / Next Up / Playing library filters.
- Stats view with launcher-tracked and account-history totals kept separate.
- Fullscreen Xbox-controller mode:
  - D-pad: navigate
  - A: play
  - Y: favorite/unfavorite
  - X: cycle All / Favorites / Next Up / Playing
  - B: exit Controller Mode
  - keyboard arrows / Enter / F / Tab / Esc are available as fallback controls.
- ZIP backup of all local launcher data.
- CSV export of merged Gaming History.
- GitHub Release update checking and installer download.
- Per-user Windows installer plus self-contained portable ZIP release artifacts.

## Install

Official releases are published from this repository.

Each release can contain:

- `GameLauncher-Setup.exe` — per-user Windows installer.
- `GameLauncher-Portable-win-x64.zip` — self-contained portable build.

The application installs under the current user's Local AppData and does not require administrator rights.

## Local data

Launcher data remains local under:

```text
%LocalAppData%\MyGameLauncher\
```

This includes the SQLite database, cached covers, and other launcher-local files.

Use **Tools → Create backup ZIP** before moving PCs or making major changes.

## Updates

**Tools → Check for updates** reads this repository's latest GitHub Release.

If a newer release contains `GameLauncher-Setup.exe`, the launcher can download it and start the installer after explicit confirmation.

## Controller Mode

Use **Controller** from the desktop launcher.

The fullscreen interface is optimized for an Xbox controller and uses Windows XInput directly. No separate controller framework is required.

The fullscreen launcher minimizes while the selected game runs and returns after the tracked session ends.

## Favorites and filtering

Favorites are stored by conservative normalized game title, not by a transient store installation ID. This helps them survive rescans and cross-store merges.

Desktop filters:

- All games
- Favorites
- Next Up
- Playing

Search matches game title, store/source, and Personal Library platform metadata.

## Personal Game Library

Use **Personal Library** to link a Google Sheet containing your broader game collection. The launcher reads a CSV export from a link-accessible or published Sheet and caches it locally.

Recommended columns are `Title`, `Platform`, `Steam App ID`, `Rank`, `Status`, `Progress`, and `Rating`. Rank/status/progress remain Sheet-owned; ratings set in the launcher are stored locally and take precedence in the launcher UI.

## Gaming History

### Steam

Steam owned-game history can sync through Valve's `IPlayerService/GetOwnedGames` Web API.

The launcher stores the Steam Web API key using Windows DPAPI for the current Windows user. It never asks for your Steam password.

### PlayStation

PlayStation Data Access `.xlsx` files can be imported directly when they contain a recognizable game-history worksheet.

### Other stores/accounts

CSV/JSON history files can be imported for Xbox/Microsoft, GOG, EA, Ubisoft, Battle.net, Epic, PlayStation, and Steam fallback workflows.

Imported account playtime is intentionally kept separate from launcher-tracked playtime to avoid pretending the two measurements are non-overlapping.

## Graphics Optimizer

Default target:

- 1920×1080
- 60 FPS
- Quality preference

Verified safe automatic config editing remains deliberately narrow. Unsupported games receive recommendations only.

## Gameplay overlay

RTSS must be installed to display the gameplay overlay. MSI Afterburner is not required.

Default OSD:

```text
FPS 60  |  GPU 73%  |  TEMP 65C
```

## Build from source

Requirements:

- Windows 10/11
- .NET 10 SDK

```powershell
.\build.ps1
.\run.ps1
```

## Release process

Push a version tag such as:

```text
v1.0.0
```

The release workflow:

1. runs the full tests;
2. publishes a self-contained `win-x64` build;
3. creates `GameLauncher-Setup.exe` with Inno Setup;
4. creates the portable ZIP;
5. publishes both artifacts as a GitHub Release.

## Project plan

- Phase 0: Foundation — complete
- Phase 1: Core MVP — complete
- Phase 2: Unified Library — complete
- Phase 3: Smart Launching — complete
- Phase 4: Gameplay Overlay — complete
- Phase 5: Hatchable Sync — superseded in v1.1 by Personal Game Library
- Phase 6: Graphics Optimizer — complete
- Phase 7: History + Console Accounts — complete
- **Phase 8: Final Product — complete**
- v1.1: UI/branding polish, overlay fixes, Personal Game Library, local ratings

See `PHASE8_NOTES.md` for implementation and safety boundaries.

## License

MIT. See `LICENSE`.
