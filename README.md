# Game Launcher

A lightweight, open-source Windows game launcher built around a local unified library. Current implementation: **Phase 4 - Gameplay Overlay**.

## Current features

- Unified installed-game discovery for Steam, Epic, GOG, EA app, Ubisoft Connect, Battle.net, and Xbox / Microsoft Store `XboxGames` installs.
- Manual game entries.
- Conservative cross-store duplicate reconciliation.
- Automatic/local cover-art caching.
- Game launching with process detection and persisted playtime.
- Per-game smart launch profiles with pre-launch, companion, and post-game programs.
- **Gameplay-only RTSS overlay** with:
  - FPS
  - GPU usage
  - GPU temperature
  - automatic start after the real game process is detected
  - automatic removal when the tracked game exits
  - configurable 250 ms to 2 second refresh interval
  - independent telemetry toggles
- WPF unified-library UI, launch-profile editor, and overlay settings.

## Gameplay overlay

Open **Overlay** in the launcher to configure the HUD.

Default display:

```text
FPS 60  |  GPU 73%  |  TEMP 65C
```

The launcher reads FPS from RTSS for the **specific tracked game process** and GPU load/temperature from LibreHardwareMonitor.

### RTSS requirement

RivaTuner Statistics Server (RTSS) must be installed on Windows for the in-game OSD. It is **not bundled** with this repository.

If RTSS is installed but not running, the launcher starts it automatically when a game session begins. RTSS may remain running after the game, but the Game Launcher OSD slot is cleared as soon as the tracked game process exits.

MSI Afterburner is not required.

If RTSS or hardware telemetry is unavailable, the overlay fails safely and the game still launches and playtime still tracks.

## Stack

- C# / .NET 10 LTS
- WPF
- SQLite via `Microsoft.Data.Sqlite`
- LibreHardwareMonitorLib 0.9.6 for GPU telemetry
- RTSS `RTSSSharedMemoryV2` for per-process FPS and in-game OSD
- No ORM, DI framework, MVVM framework, account credentials, or private launcher APIs.

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

Local data:

```text
%LocalAppData%\MyGameLauncher\
```

## Project plan

- Phase 0: Foundation - complete
- Phase 1: Core MVP - complete
- Phase 2: Unified Library - complete
- Phase 3: Smart Launching - complete
- **Phase 4: Gameplay Overlay - current**
- Phase 5: Hatchable Sync
- Phase 6: Graphics Optimizer
- Phase 7: History + Console Accounts
- Phase 8: Final Product

See `PHASE4_NOTES.md` for implementation boundaries.

## License

MIT. See `LICENSE`.
