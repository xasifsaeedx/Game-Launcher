# Game Launcher

A lightweight, open-source Windows game launcher built around a local unified library. Current implementation: **Phase 5 - Hatchable Sync**.

## Current features

- Installed-game discovery for Steam, Epic, GOG, EA app, Ubisoft Connect, Battle.net, and Xbox / Microsoft Store `XboxGames` installs.
- Manual game entries.
- Conservative cross-store duplicate reconciliation.
- Automatic/local cover-art caching.
- Game launching with process detection and persisted playtime.
- Per-game smart launch profiles with pre-launch, companion, and post-game programs.
- Gameplay-only RTSS overlay with FPS, GPU usage, and GPU temperature.
- **Hatchable synchronization** with the companion My Game Library site:
  - pulls the ranked Next 100 list
  - matches installed games by Steam App ID first, exact normalized title second
  - shows Next 100 rank/status on installed game cards
  - provides a full Next Play window for ranked games that are installed or not installed
  - uploads cumulative tracked playtime and last-played time
  - syncs Playing / Completed / Paused / Dropped progress
  - syncs optional 1-10 ratings
  - automatically syncs at startup and after gameplay when enabled
  - keeps a local cached ranking for offline viewing
  - stores the launcher token encrypted with Windows DPAPI

## Hatchable Sync setup

The default companion site is:

```text
https://my-game-library.hatchable.site
```

1. Open **Hatchable** in the launcher.
2. Click **Open Launcher Sync page**.
3. Sign in to the Hatchable site and create a launcher token.
4. Copy the token into the launcher.
5. Choose whether automatic sync should run.
6. Click **Save & test**.

The site stores only a SHA-256 hash of the device token. The Windows launcher stores the token encrypted for the current Windows user through DPAPI.

### Sync ownership rules

- The Hatchable site remains authoritative for **Next 100 ranking** and the existing **Played / Play next / Didn't like** preference.
- The launcher contributes cumulative **playtime** and **last played**.
- Progress state and 1-10 rating may be edited from either the website or the launcher.
- Local tracked playtime never decreases the remote total.
- Starting a locally matched game with tracked playtime can set progress to **Playing** when no progress has been chosen yet.
- Marking progress **Completed** updates the website preference to **Played**, except an explicit **Didn't like** preference is preserved.
- Network/sync failures never block launching or local playtime tracking.

## Gameplay overlay

RTSS must be installed for the in-game OSD. MSI Afterburner is not required.

Default display:

```text
FPS 60  |  GPU 73%  |  TEMP 65C
```

## Stack

- C# / .NET 10 LTS
- WPF
- SQLite via `Microsoft.Data.Sqlite`
- LibreHardwareMonitorLib 0.9.6
- RTSS `RTSSSharedMemoryV2`
- Windows DPAPI via `System.Security.Cryptography.ProtectedData`
- Hatchable companion web application/API
- No ORM, DI framework, MVVM framework, or private launcher APIs.

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
- **Phase 5: Hatchable Sync - current**
- Phase 6: Graphics Optimizer
- Phase 7: History + Console Accounts
- Phase 8: Final Product

See `PHASE5_NOTES.md` for implementation boundaries.

## License

MIT. See `LICENSE`.
