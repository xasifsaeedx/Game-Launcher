# My Game Launcher - Phase 0

Phase 0 implements the foundation from the build plan:

- C# / .NET 10 LTS
- WPF Windows desktop shell
- SQLite via Microsoft.Data.Sqlite
- Game, installation, play-session, and process-rule data model
- Internal launcher-adapter interface
- Demo adapter proving the adapter seam
- SQLite repository proving persistence
- Dependency-light smoke-test executable

## Architecture

```text
GameLauncher.App (WPF)
        |
        v
GameLauncher.Core (models, interfaces, service)
        ^
        |
GameLauncher.Infrastructure (SQLite + adapters)
```

No ORM, dependency-injection framework, dynamic DLL plugin loader, or MVVM framework is included in Phase 0.

## Requirements

- Windows 10/11
- .NET 10 SDK

.NET 10 is used because it is the current LTS release for this build.

## Build and test

Open PowerShell in this folder and run:

```powershell
.\build.ps1
```

Or manually:

```powershell
dotnet restore .\GameLauncher.sln
dotnet build .\GameLauncher.sln -c Release
dotnet run --project .\tests\GameLauncher.Tests\GameLauncher.Tests.csproj -c Release
dotnet run --project .\src\GameLauncher.App\GameLauncher.App.csproj
```

## Phase 0 acceptance criteria

When the app starts successfully:

1. `%LocalAppData%\MyGameLauncher\` is created.
2. `launcher.db`, `logs`, and `cache` are available under that folder.
3. SQLite creates tables for games, installations, play sessions, and process rules.
4. The demo launcher adapter returns one deterministic test game.
5. Running the foundation check repeatedly does not duplicate that game.
6. The WPF shell lists the stored test game and reports a ready status.

The demo adapter is deliberately temporary. Phase 1 replaces it with manual-game support and the first real Steam adapter while keeping the same interface.

## License

MIT. See `LICENSE`.
