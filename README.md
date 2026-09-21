# Game Launcher

A lightweight, open-source Windows game launcher built around a local unified library. Current implementation: **Phase 1 - Core MVP**.

## Current features

- Manual game library entries.
- Automatic installed Steam-game discovery from local Steam files.
- Launch manual games and Steam games.
- Detect the launched game process and track session playtime.
- Persist library and play sessions in local SQLite.
- Optional manual cover images.
- Basic WPF cover-grid library UI.
- Repeated Steam sync without duplicate Steam records.

## Stack

- C# / .NET 10 LTS
- WPF
- SQLite via `Microsoft.Data.Sqlite`
- No ORM, DI framework, MVVM framework, dynamic plugin loader, or web service dependency.

## Build

Requirements: Windows 10/11 and the .NET 10 SDK.

```powershell
.\build.ps1
```

Run the launcher:

```powershell
.\run.ps1
```

The app stores its local data under:

```text
%LocalAppData%\MyGameLauncher\
```

## Phase 1 behavior

On startup the launcher checks the local Steam installation and reads Steam library manifests. Steam launch uses `steam://rungameid/<appid>`. Because Steam manifests do not reliably provide a game executable, the launcher detects the newly started process located under that game's Steam installation folder and then records the session until that process exits.

Manual games use the executable selected when the game is added.

## Project plan

- Phase 0: Foundation - complete
- **Phase 1: Core MVP - current**
- Phase 2: Unified Library
- Phase 3: Smart Launching
- Phase 4: Gameplay Overlay
- Phase 5: Hatchable Sync
- Phase 6: Graphics Optimizer
- Phase 7: History + Console Accounts
- Phase 8: Final Product

See `PHASE1_NOTES.md` for current implementation boundaries.

## License

MIT. See `LICENSE`.
