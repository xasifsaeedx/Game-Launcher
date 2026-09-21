using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Runtime;
using GameLauncher.Core.Services;
using GameLauncher.Core.Utilities;
using GameLauncher.Infrastructure.Adapters;
using GameLauncher.Infrastructure.Repositories;
using GameLauncher.Infrastructure.Storage;

var tests = new (string Name, Func<Task> Run)[]
{
    ("AppPaths creates expected folders", AppPathsCreatesExpectedFolders),
    ("Stable IDs are deterministic", StableIdsAreDeterministic),
    ("Steam manifest parser reads required fields", SteamManifestParserReadsFields),
    ("Steam library parser reads extra libraries", SteamLibraryParserReadsPaths),
    ("SQLite repository round-trips Phase 1 fields", RepositoryRoundTripsPhase1Fields),
    ("SQLite repository totals completed play sessions", RepositoryTotalsPlaySessions),
    ("Library service adds a manual game", LibraryServiceAddsManualGame),
    ("Source sync remains idempotent", SourceSyncRemainsIdempotent),
    ("Session service persists runtime playtime", SessionServicePersistsPlaytime)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL  {test.Name}: {ex.Message}");
        Console.WriteLine(failures[^1]);
    }
}

if (failures.Count > 0)
{
    Environment.ExitCode = 1;
    Console.WriteLine($"{failures.Count} test(s) failed.");
}
else
{
    Console.WriteLine($"All {tests.Length} Phase 1 tests passed.");
}

static Task AppPathsCreatesExpectedFolders()
{
    using var temp = new TempDirectory();
    var paths = new AppPaths(temp.Path);
    paths.EnsureCreated();

    Assert.True(Directory.Exists(paths.RootDirectory));
    Assert.True(Directory.Exists(paths.LogsDirectory));
    Assert.True(Directory.Exists(paths.CacheDirectory));
    Assert.Equal(Path.Combine(paths.RootDirectory, "launcher.db"), paths.DatabasePath);
    return Task.CompletedTask;
}

static Task StableIdsAreDeterministic()
{
    var first = StableId.FromText("steam-game:205100");
    var second = StableId.FromText("steam-game:205100");
    var other = StableId.FromText("steam-game:292030");

    Assert.Equal(first, second);
    Assert.NotEqual(first, other);
    return Task.CompletedTask;
}

static Task SteamManifestParserReadsFields()
{
    const string manifest = """
        "AppState"
        {
            "appid"        "205100"
            "name"         "Dishonored"
            "installdir"   "Dishonored"
            "StateFlags"   "4"
        }
        """;

    var parsed = SteamManifestParser.Parse(manifest);
    Assert.NotNull(parsed);
    Assert.Equal("205100", parsed!.AppId);
    Assert.Equal("Dishonored", parsed.Name);
    Assert.Equal("Dishonored", parsed.InstallDirectoryName);
    return Task.CompletedTask;
}

static Task SteamLibraryParserReadsPaths()
{
    var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SteamRoot"));
    var second = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SteamLibrary"));
    var escaped = second.Replace("\\", "\\\\", StringComparison.Ordinal);
    var vdf = $"\"libraryfolders\"\n{{\n\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{escaped}\"\n\t}}\n}}";

    var paths = SteamLibraryParser.ParseLibraryPaths(vdf, root);
    Assert.True(paths.Contains(root, StringComparer.OrdinalIgnoreCase));
    Assert.True(paths.Contains(second, StringComparer.OrdinalIgnoreCase));
    return Task.CompletedTask;
}

static async Task RepositoryRoundTripsPhase1Fields()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));
    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Control", now, now, @"C:\Covers\control.jpg");
    await repository.UpsertGameAsync(game);

    var installation = new GameInstallation(
        Guid.NewGuid(),
        game.Id,
        GameSource.Manual,
        "manual-control",
        @"C:\Games\Control",
        @"C:\Games\Control\Control.exe",
        null,
        true,
        "-dx12");
    await repository.UpsertInstallationAsync(installation);

    var stored = await repository.GetGameAsync(game.Id);
    var installations = await repository.GetInstallationsAsync(game.Id);

    Assert.NotNull(stored);
    Assert.Equal(game.CoverImagePath, stored!.CoverImagePath);
    Assert.Equal(1, installations.Count);
    Assert.Equal("-dx12", installations[0].LaunchArguments);
}

static async Task RepositoryTotalsPlaySessions()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));
    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Test", now, now);
    await repository.UpsertGameAsync(game);

    var session = new PlaySession(Guid.NewGuid(), game.Id, now, null, null);
    await repository.AddPlaySessionAsync(session);
    await repository.EndPlaySessionAsync(session.Id, now.AddMinutes(10), 600);

    Assert.Equal(600L, await repository.GetTotalPlaytimeSecondsAsync(game.Id));
    Assert.NotNull(await repository.GetLastPlayedUtcAsync(game.Id));
}

static async Task LibraryServiceAddsManualGame()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));
    var service = new GameLibraryService(repository, Array.Empty<IGameSourceAdapter>());
    var fakeExe = Path.Combine(temp.Path, "Game.exe");

    var added = await service.AddManualGameAsync("My Game", fakeExe, null, "-windowed");
    var library = await service.GetLibraryAsync();

    Assert.Equal(GameSource.Manual, added.PreferredInstallation!.Source);
    Assert.Equal(1, library.Count);
    Assert.Equal("My Game", library[0].Game.Title);
    Assert.Equal("-windowed", library[0].PreferredInstallation!.LaunchArguments);
}

static async Task SourceSyncRemainsIdempotent()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));
    var service = new GameLibraryService(repository, new[] { new FixedAdapter() });

    await service.SyncSourcesAsync();
    await service.SyncSourcesAsync();
    var library = await service.GetLibraryAsync();

    Assert.Equal(1, library.Count);
    Assert.Equal(1, library[0].Installations.Count);
}

static async Task SessionServicePersistsPlaytime()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));
    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Runtime Test", now, now);
    var installation = new GameInstallation(
        Guid.NewGuid(), game.Id, GameSource.Manual, "runtime-test",
        temp.Path, Path.Combine(temp.Path, "Runtime.exe"), null, true);
    await repository.UpsertGameAsync(game);
    await repository.UpsertInstallationAsync(installation);

    var sessions = new GameSessionService(repository, new FakeRuntime(now, now.AddSeconds(125)));
    var completed = await sessions.LaunchAndTrackAsync(installation);

    Assert.Equal(125L, completed.DurationSeconds);
    Assert.Equal(125L, await repository.GetTotalPlaytimeSecondsAsync(game.Id));
}

file sealed class FixedAdapter : IGameSourceAdapter
{
    private static readonly Guid GameId = StableId.FromText("test-game");
    private static readonly Guid InstallationId = StableId.FromText("test-install");

    public string Id => "test";
    public string DisplayName => "Test Adapter";
    public GameSource Source => GameSource.Steam;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game(GameId, "Phase 1 Test Game", now, now);
        var install = new GameInstallation(
            InstallationId, GameId, GameSource.Steam, "12345",
            @"C:\Games\Phase1", null, "steam://rungameid/12345", true);

        IReadOnlyList<DiscoveredGame> result = new[] { new DiscoveredGame(game, install) };
        return Task.FromResult(result);
    }
}

file sealed class FakeRuntime : IGameRuntime
{
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _end;

    public FakeRuntime(DateTimeOffset start, DateTimeOffset end)
    {
        _start = start;
        _end = end;
    }

    public Task<IGameRunHandle> LaunchAsync(GameInstallation installation, CancellationToken cancellationToken = default) =>
        Task.FromResult<IGameRunHandle>(new FakeRunHandle(_start, _end, installation.ExecutablePath));
}

file sealed class FakeRunHandle : IGameRunHandle
{
    private readonly DateTimeOffset _end;

    public FakeRunHandle(DateTimeOffset start, DateTimeOffset end, string? executablePath)
    {
        StartedUtc = start;
        _end = end;
        DetectedExecutablePath = executablePath;
    }

    public DateTimeOffset StartedUtc { get; }
    public string? DetectedExecutablePath { get; }
    public Task<DateTimeOffset> WaitForExitAsync(CancellationToken cancellationToken = default) => Task.FromResult(_end);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

file static class Assert
{
    public static void True(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Expected condition to be true.");
    }

    public static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    public static void NotEqual<T>(T notExpected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(notExpected, actual))
        {
            throw new InvalidOperationException($"Did not expect '{actual}'.");
        }
    }

    public static void NotNull(object? value)
    {
        if (value is null) throw new InvalidOperationException("Expected a non-null value.");
    }
}

file sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GameLauncherTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
