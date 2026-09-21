# Game Launcher

A lightweight, open-source Windows game launcher built around a local unified library. Current implementation: **Phase 3 - Smart Launching**.

## Current features

- Unified installed-game discovery for Steam, Epic, GOG, EA app, Ubisoft Connect, Battle.net, and Xbox / Microsoft Store `XboxGames` installs.
- Manual game entries.
- Conservative cross-store duplicate reconciliation.
- Automatic/local cover-art caching.
- Game launching with process detection and persisted playtime.
- **Per-game launch profiles** with:
  - selectable game installation
  - game argument overrides
  - ordered pre-launch programs
  - background companion programs
  - optional automatic companion shutdown when the game exits
  - ordered post-game programs
  - multiple profiles per game with one default one-click profile
- WPF cover-grid unified library UI and profile editor.

## Smart launching

Open **Profiles** on any game card to create a launch profile. A profile can start tools such as monitoring, controller, streaming, audio, or other helper applications around the game.

The normal **Play** button uses the profile marked **Default one-click profile**. If no default profile exists, Play behaves exactly like Phase 2 and launches the preferred game installation directly.

For pre/post actions, **Wait** means the launcher waits for that program to finish before continuing. For companion actions, **Close** means the launcher terminates that companion after the tracked game process exits.

## Stack

- C# / .NET 10 LTS
- WPF
- SQLite via `Microsoft.Data.Sqlite`
- Local launcher manifests/registry discovery
- No ORM, DI framework, MVVM framework, account credentials, or private launcher APIs.

## Build

Requirements: Windows 10/11 and the .NET 10 SDK.

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
- **Phase 3: Smart Launching - current**
- Phase 4: Gameplay Overlay
- Phase 5: Hatchable Sync
- Phase 6: Graphics Optimizer
- Phase 7: History + Console Accounts
- Phase 8: Final Product

See `PHASE3_NOTES.md` for implementation boundaries.

## License

MIT. See `LICENSE`.
