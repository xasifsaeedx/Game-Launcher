using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;
using GameLauncher.Infrastructure.Repositories;
using GameLauncher.Infrastructure.Storage;

var tests = new (string Name, Func<Task> Run)[]
{
    ("AppPaths creates expected folders", AppPathsCreatesExpectedFolders),
    ("SQLite repository round-trips a game", RepositoryRoundTripsGame),
    ("SQLite repository upserts an installation", RepositoryUpsertsInstallation),
    ("Library service discovers and stores without duplicates", ServiceDiscoversAndStoresWithoutDuplicates)
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
    Console.WriteLine($"All {tests.Length} Phase 0 tests passed.");
}

static Task AppPathsCreatesExpectedFolders()
{
    using var temp = new TempDirectory();
    var paths = new AppPaths(temp.Path);
    paths.EnsureCreated();

    Assert.Equal(System.IO.Path.Combine(temp.Path, "MyGameLauncher"), paths.RootDirectory);
    Assert.True(Directory.Exists(paths.RootDirectory));
    Assert.True(Directory.Exists(paths.LogsDirectory));
    Assert.True(Directory.Exists(paths.CacheDirectory));
    Assert.Equal(System.IO.Path.Combine(paths.RootDirectory, "launcher.db"), paths.DatabasePath);
    return Task.CompletedTask;
}

static async Task RepositoryRoundTripsGame()
{
    using var temp = new TempDirectory();
    var dbPath = System.IO.Path.Combine(temp.Path, "launcher.db");
    var repository = new SqliteGameRepository(dbPath);
    await repository.InitializeAsync();

    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Control", now, now);
    await repository.UpsertGameAsync(game);

    var games = await repository.GetGamesAsync();
    Assert.Equal(1, games.Count);
    Assert.Equal(game.Id, games[0].Id);
    Assert.Equal("Control", games[0].Title);
}

static async Task RepositoryUpsertsInstallation()
{
    using var temp = new TempDirectory();
    var dbPath = System.IO.Path.Combine(temp.Path, "launcher.db");
    var repository = new SqliteGameRepository(dbPath);
    await repository.InitializeAsync();

    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Dishonored", now, now);
    await repository.UpsertGameAsync(game);

    var installationId = Guid.NewGuid();
    var first = new GameInstallation(
        installationId,
        game.Id,
        GameSource.Steam,
        "steam-205100",
        @"C:\Games\Dishonored",
        @"C:\Games\Dishonored\Dishonored.exe",
        "steam://rungameid/205100",
        true);

    await repository.UpsertInstallationAsync(first);
    var updated = first with { ExecutablePath = @"D:\Games\Dishonored\Dishonored.exe" };
    await repository.UpsertInstallationAsync(updated);

    var installations = await repository.GetInstallationsAsync(game.Id);
    Assert.Equal(1, installations.Count);
    Assert.Equal(updated.ExecutablePath, installations[0].ExecutablePath);
}

static async Task ServiceDiscoversAndStoresWithoutDuplicates()
{
    using var temp = new TempDirectory();
    var dbPath = System.IO.Path.Combine(temp.Path, "launcher.db");
    var repository = new SqliteGameRepository(dbPath);
    var adapter = new FixedAdapter();
    var service = new GameLibraryService(repository, new[] { adapter });

    var first = await service.DiscoverAndStoreAsync();
    var second = await service.DiscoverAndStoreAsync();
    var games = await service.GetGamesAsync();

    Assert.Equal(1, first.DiscoveredCount);
    Assert.Equal(1, second.DiscoveredCount);
    Assert.Equal(1, games.Count);
    Assert.Equal("Phase 0 Test Game", games[0].Title);
}

file sealed class FixedAdapter : IGameSourceAdapter
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid InstallationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public string Id => "test";
    public string DisplayName => "Test Adapter";
    public GameSource Source => GameSource.Demo;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game(GameId, "Phase 0 Test Game", now, now);
        var install = new GameInstallation(
            InstallationId,
            GameId,
            GameSource.Demo,
            "phase0-test",
            @"C:\Games\Phase0Test",
            @"C:\Games\Phase0Test\Phase0Test.exe",
            null,
            true);

        IReadOnlyList<DiscoveredGame> result = new[] { new DiscoveredGame(game, install) };
        return Task.FromResult(result);
    }
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
}

file sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GameLauncherPhase0Tests", Guid.NewGuid().ToString("N"));
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
            // Best-effort test cleanup.
        }
    }
}
