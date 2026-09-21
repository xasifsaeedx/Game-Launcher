# Game Launcher

A lightweight, open-source Windows game launcher built around a local unified library. Current implementation: **Phase 2 - Unified Library**.

## Current features

- Manual game library entries.
- Automatic installed-game discovery for:
  - Steam
  - Epic Games
  - GOG
  - EA app
  - Ubisoft Connect
  - Battle.net
  - Xbox / Microsoft Store games using the modern `XboxGames` install layout
- Conservative cross-store duplicate reconciliation using normalized exact titles.
- Launch games from their discovered executable or launcher URI.
- Detect launched game processes and track session playtime.
- Persist library and play sessions in local SQLite.
- Automatic cover-art caching:
  - direct Steam library artwork for Steam games
  - exact-title Steam Store lookup as a credential-free fallback for matching non-Steam games
  - local artwork discovered inside game folders when available
- Basic WPF cover-grid unified library UI showing all detected sources for a title.

## Stack

- C# / .NET 10 LTS
- WPF
- SQLite via `Microsoft.Data.Sqlite`
- Local Windows registry/manifests for launcher discovery
- No ORM, DI framework, MVVM framework, account credentials, or private launcher APIs.

## Build

Requirements: Windows 10/11 and the .NET 10 SDK.

```powershell
.\build.ps1
```

Run the launcher:

```powershell
.\run.ps1
```

Local data:

```text
%LocalAppData%\MyGameLauncher\
```

Downloaded covers are cached under:

```text
%LocalAppData%\MyGameLauncher\cache\covers\
```

## Discovery notes

Discovery is intentionally local-first and credential-free. The launcher reads installed-game manifests, registry entries, or install layouts already present on the PC.

Xbox / Microsoft Store coverage targets games installed through the modern Xbox app layout under an `XboxGames` folder. It does not scrape protected `WindowsApps` UWP packages.

Cross-store merging is conservative: only strong normalized-title equality is merged automatically. Different editions such as `Control` and `Control Ultimate Edition` remain separate.

## Project plan

- Phase 0: Foundation - complete
- Phase 1: Core MVP - complete
- **Phase 2: Unified Library - current**
- Phase 3: Smart Launching
- Phase 4: Gameplay Overlay
- Phase 5: Hatchable Sync
- Phase 6: Graphics Optimizer
- Phase 7: History + Console Accounts
- Phase 8: Final Product

See `PHASE2_NOTES.md` for implementation boundaries.

## License

MIT. See `LICENSE`.
