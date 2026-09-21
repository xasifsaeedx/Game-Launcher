# Phase 8 — Final Product

## Goal

Turn the Phase 0–7 feature set into a coherent v1.0 launcher without replacing the lightweight architecture.

Phase 8 adds presentation, persistence, maintenance, packaging, and update workflows around the existing library/services.

## Desktop library

The main library now includes:

- live text search
- All / Favorites / Next Up / Playing filters
- persistent favorite toggles
- Stats
- Controller Mode
- History
- Graphics Optimizer
- Next Play
- Overlay
- Tools
- library rescan

Favorites use the normalized game title as their durable key rather than a specific installation ID.

## Controller Mode

Controller Mode is a separate borderless maximized WPF window.

Input uses Windows XInput directly.

Controls:

- D-pad: grid navigation
- A: launch selected game
- Y: toggle favorite
- X: cycle view
- B: close Controller Mode

Keyboard fallback:

- arrows: navigation
- Enter: launch
- F: favorite
- Tab: cycle view
- Escape: exit

The window minimizes while a game is running. It restores after the tracked session ends.

Desktop and Controller Mode both use the same `LauncherPlayService`, so graphics auto-apply, smart launching, playtime persistence, and Hatchable post-session sync follow one behavior path.

## Stats

Stats reports:

- installed games
- favorites
- Next Up/Playing count
- historical-game count
- launcher-tracked playtime
- imported account playtime
- most-played locally tracked game
- most-recent locally tracked game

Account-history playtime and launcher-tracked playtime remain separate; the UI does not add them together.

## Backup/export

`LauncherBackupService` supports:

### Full backup ZIP

The ZIP contains the local launcher root under:

```text
%LocalAppData%\MyGameLauncher
```

Files are opened with read sharing so the SQLite file and caches can be copied while the launcher is open.

### Gaming History CSV

The export includes:

- title
- imported sources
- known account playtime seconds
- launcher-tracked seconds
- last played UTC
- currently installed flag

CSV fields are quoted/escaped.

Phase 8 intentionally does not perform an in-place restore while the application is running. A ZIP backup is the portable recovery artifact.

## Updates

`GitHubReleaseUpdateService` checks:

```text
https://api.github.com/repos/xasifsaeedx/Game-Launcher/releases/latest
```

Update rules:

- installed assembly version is compared with the latest release tag;
- release tags must parse as ordinary versions such as `v1.1.0`;
- the updater looks specifically for `GameLauncher-Setup.exe`;
- the user must explicitly confirm before download/install;
- downloaded installers go to a versioned temp directory;
- the launcher closes only after successfully starting the installer.

## Installer

The installer is defined in:

```text
installer/GameLauncher.iss
```

Characteristics:

- per-user install
- no administrator rights required
- Local AppData program directory
- Start-menu shortcut
- optional desktop shortcut
- standard uninstall entry

## GitHub release workflow

`.github/workflows/release.yml` runs on `v*` tags.

It:

1. restores and tests the solution;
2. publishes a self-contained Windows x64 application;
3. installs Inno Setup on the CI runner;
4. creates `GameLauncher-Setup.exe`;
5. creates `GameLauncher-Portable-win-x64.zip`;
6. creates the GitHub Release with both assets.

The in-app updater's expected installer filename is therefore part of the release contract and regression coverage.

## Intentional limits

- v1.0 targets Windows x64.
- Controller Mode targets XInput-compatible controllers, especially Xbox controllers.
- No background updater silently installs software.
- No admin-level installer is required.
- No automatic restore overwrites an active database.
- No cloud backup is introduced; backup stays user-controlled and local.
- Search/filtering remains deliberately simple and local.
- No Electron/webview layer or extra UI framework is introduced.
