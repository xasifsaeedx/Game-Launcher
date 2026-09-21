# Game Launcher

A lightweight, open-source Windows game launcher built around a local unified library. Current implementation: **Phase 6 - Graphics Optimizer**.

## Current features

- Installed-game discovery for Steam, Epic, GOG, EA app, Ubisoft Connect, Battle.net, and Xbox / Microsoft Store `XboxGames` installs.
- Manual game entries.
- Conservative cross-store duplicate reconciliation.
- Automatic/local cover-art caching.
- Game launching with process detection and persisted playtime.
- Per-game smart launch profiles with pre-launch, companion, and post-game programs.
- Gameplay-only RTSS overlay with FPS, GPU usage, and GPU temperature.
- Hatchable synchronization for Next 100 ranking, status, progress, rating, playtime, and last-played data.
- **Graphics Optimizer**:
  - detects CPU, GPU, RAM, primary resolution, and refresh rate
  - classifies the GPU into a conservative performance tier
  - defaults to a 1920x1080 / 60 FPS / quality-oriented target
  - supports Performance / Balanced / Quality preferences
  - allows manual tier override for unknown or future GPUs
  - provides per-game recommendations
  - previews every field that can be changed automatically
  - backs up original config before the first automatic change
  - can restore the original config
  - never creates missing settings or rewrites unsupported games
  - can optionally apply verified safe fields immediately before launch

## Graphics Optimizer safety

Automatic config editing is intentionally narrow.

Current verified safe-apply profiles:

- The Witcher 3: Wild Hunt
- Grand Theft Auto V
- Red Dead Redemption 2

Current recommendation-only profiles include:

- Control: Ultimate Edition
- God of War
- Ghost of Tsushima DIRECTOR'S CUT
- Assassin's Creed Shadows
- Black Myth: Wukong

Games without a built-in profile still receive generic recommendations from the selected hardware tier and target, but the launcher will not edit their configuration files.

Automatic writers only modify known fields that already exist in a recognized config file. The first modification creates a `.game-launcher.bak` copy beside the original file. Later applies never overwrite that original backup.

If automatic apply is enabled and an optimization step fails, the game launch still continues with the existing configuration.

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
- Phase 5: Hatchable Sync - complete
- **Phase 6: Graphics Optimizer - current**
- Phase 7: History + Console Accounts
- Phase 8: Final Product

See `PHASE6_NOTES.md` for implementation boundaries.

## License

MIT. See `LICENSE`.
