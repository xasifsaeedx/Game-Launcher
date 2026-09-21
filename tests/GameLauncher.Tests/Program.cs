using System.Net;
using System.Net.Http;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Runtime;
using GameLauncher.Core.Services;
using GameLauncher.Core.Utilities;
using GameLauncher.Infrastructure.Adapters;
using GameLauncher.Infrastructure.Metadata;
using GameLauncher.Infrastructure.Repositories;
using GameLauncher.Infrastructure.Storage;

var tests = new (string Name, Func<Task> Run)[]
{
    ("AppPaths creates expected folders", AppPathsCreatesExpectedFolders),
    ("Stable IDs are deterministic", StableIdsAreDeterministic),
    ("Title normalization is conservative", TitleNormalizationIsConservative),
    ("Steam manifest parser reads required fields", SteamManifestParserReadsFields),
    ("Steam library parser reads extra libraries", SteamLibraryParserReadsPaths),
    ("Epic manifest parser reads installed game metadata", EpicManifestParserReadsFields),
    ("Epic adapter discovers local installation", EpicAdapterDiscoversInstallation),
    ("Xbox adapter discovers XboxGames installation", XboxAdapterDiscoversInstallation),
    ("SQLite repository round-trips launcher fields", RepositoryRoundTripsFields),
    ("SQLite repository totals completed play sessions", RepositoryTotalsPlaySessions),
    ("Library service adds a manual game", LibraryServiceAddsManualGame),
    ("Source sync remains idempotent", SourceSyncRemainsIdempotent),
    ("Source rescan retires removed installations", SourceRescanRetiresRemovedInstallations),
    ("Unified library merges exact normalized titles", UnifiedLibraryMergesExactTitles),
    ("Steam artwork enricher caches cover art", SteamArtworkEnricherCachesCover),
    ("Launch profile repository persists profile and actions", LaunchProfileRepositoryPersistsProfileAndActions),
    ("Launch profile service keeps one default across merged installs", LaunchProfileServiceKeepsOneDefault),
    ("Smart launch orders actions and cleans companions", SmartLaunchOrdersActionsAndCleansCompanions),
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
    Console.WriteLine($"All {tests.Length} Phase 3 tests passed.");
}

static Task AppPathsCreatesExpectedFolders()
{
    using var temp = new TempDirectory();
    var paths = new AppPaths(temp.Path);
    paths.EnsureCreated();

    Assert.True(Directory.Exists(paths.RootDirectory));
    Assert.True(Directory.Exists(paths.LogsDirectory));
    Assert.True(Directory.Exists(paths.CacheDirectory));
    Assert.True(Directory.Exists(paths.CoversDirectory));
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

static Task TitleNormalizationIsConservative()
{
    Assert.Equal("control", GameTitleNormalizer.Normalize("Control™"));
    Assert.Equal("control", GameTitleNormalizer.Normalize("CONTROL [PC]"));
    Assert.NotEqual(
        GameTitleNormalizer.Normalize("Control"),
        GameTitleNormalizer.Normalize("Control Ultimate Edition"));
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

static Task EpicManifestParserReadsFields()
{
    const string json = """
        {
          "DisplayName": "Control",
          "InstallLocation": "C:\\Epic\\Control",
          "AppName": "ControlApp",
          "CatalogItemId": "catalog-control",
          "LaunchExecutable": "Control.exe",
          "LaunchCommand": "-EpicPortal"
        }
        """;

    var parsed = EpicGameSourceAdapter.ParseManifest(json);
    Assert.NotNull(parsed);
    Assert.Equal("Control", parsed!.DisplayName);
    Assert.Equal("ControlApp", parsed.AppName);
    Assert.Equal("catalog-control", parsed.CatalogItemId);
    return Task.CompletedTask;
}

static async Task EpicAdapterDiscoversInstallation()
{
    using var temp = new TempDirectory();
    var install = Path.Combine(temp.Path, "EpicLibrary", "Control");
    Directory.CreateDirectory(install);
    File.WriteAllText(Path.Combine(install, "Control.exe"), string.Empty);

    var manifestDir = Path.Combine(
        temp.Path, "Epic", "EpicGamesLauncher", "Data", "Manifests");
    Directory.CreateDirectory(manifestDir);

    var escapedInstall = install.Replace("\\", "\\\\", StringComparison.Ordinal);
    File.WriteAllText(
        Path.Combine(manifestDir, "control.item"),
        $$"""
        {
          "DisplayName": "Control",
          "InstallLocation": "{{escapedInstall}}",
          "AppName": "ControlApp",
          "CatalogItemId": "catalog-control",
          "LaunchExecutable": "Control.exe"
        }
        """);

    var adapter = new EpicGameSourceAdapter(temp.Path);
    var games = await adapter.DiscoverInstalledGamesAsync();

    Assert.Equal(1, games.Count);
    Assert.Equal(GameSource.Epic, games[0].Installation.Source);
    Assert.Equal("Control", games[0].Game.Title);
}

static async Task XboxAdapterDiscoversInstallation()
{
    using var temp = new TempDirectory();
    var content = Path.Combine(temp.Path, "XboxGames", "Forza Horizon 5", "Content");
    Directory.CreateDirectory(content);
    File.WriteAllText(Path.Combine(content, "gamelaunchhelper.exe"), string.Empty);

    var adapter = new XboxGameSourceAdapter(new[] { temp.Path });
    var games = await adapter.DiscoverInstalledGamesAsync();

    Assert.Equal(1, games.Count);
    Assert.Equal("Forza Horizon 5", games[0].Game.Title);
    Assert.Equal(GameSource.Xbox, games[0].Installation.Source);
}

static async Task RepositoryRoundTripsFields()
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
    var adapter = FixedAdapter.Create("steam", GameSource.Steam, "12345", "Phase 2 Test Game");
    var service = new GameLibraryService(repository, new[] { adapter });

    await service.SyncSourcesAsync();
    await service.SyncSourcesAsync();
    var library = await service.GetLibraryAsync();

    Assert.Equal(1, library.Count);
    Assert.Equal(1, library[0].Installations.Count);
}

static async Task SourceRescanRetiresRemovedInstallations()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));
    var adapter = new MutableAdapter(
        "epic",
        GameSource.Epic,
        "control-epic",
        "Control");

    var service = new GameLibraryService(repository, new IGameSourceAdapter[] { adapter });

    await service.SyncSourcesAsync();
    Assert.Equal(1, (await service.GetLibraryAsync()).Count);

    adapter.IsInstalled = false;
    await service.SyncSourcesAsync();

    Assert.Equal(0, (await service.GetLibraryAsync()).Count);
}

static async Task UnifiedLibraryMergesExactTitles()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));

    var steam = FixedAdapter.Create("steam", GameSource.Steam, "870780", "Control™");
    var epic = FixedAdapter.Create("epic", GameSource.Epic, "control-epic", "CONTROL [PC]");
    var service = new GameLibraryService(repository, new IGameSourceAdapter[] { steam, epic });

    await service.SyncSourcesAsync();
    var library = await service.GetLibraryAsync();

    Assert.Equal(1, library.Count);
    Assert.Equal(2, library[0].Installations.Count);
    Assert.True(library[0].Installations.Any(x => x.Source == GameSource.Steam));
    Assert.True(library[0].Installations.Any(x => x.Source == GameSource.Epic));
}

static async Task SteamArtworkEnricherCachesCover()
{
    using var temp = new TempDirectory();
    var client = new HttpClient(new StaticHttpHandler(new byte[2048]));
    var enricher = new SteamArtworkMetadataEnricher(temp.Path, client);

    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Control", now, now);
    var install = new GameInstallation(
        Guid.NewGuid(), game.Id, GameSource.Steam, "870780",
        @"C:\Steam\Control", null, "steam://rungameid/870780", true);

    var enriched = await enricher.EnrichAsync(game, new[] { install });

    Assert.NotNull(enriched.CoverImagePath);
    Assert.True(File.Exists(enriched.CoverImagePath!));
}

static async Task LaunchProfileRepositoryPersistsProfileAndActions()
{
    using var temp = new TempDirectory();
    var dbPath = Path.Combine(temp.Path, "launcher.db");
    var games = new SqliteGameRepository(dbPath);
    var profiles = new SqliteLaunchProfileRepository(dbPath);

    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Profile Test", now, now);
    await games.UpsertGameAsync(game);

    var profile = new LaunchProfile(
        Guid.NewGuid(),
        game.Id,
        "Streaming",
        null,
        "-dx12",
        true,
        now,
        now);

    var action = new LaunchAction(
        Guid.NewGuid(),
        profile.Id,
        LaunchActionStage.Companion,
        "OBS",
        @"C:\Tools\obs64.exe",
        "--startreplaybuffer",
        @"C:\Tools",
        0,
        false,
        true,
        true);

    await profiles.UpsertLaunchProfileAsync(profile);
    await profiles.ReplaceLaunchActionsAsync(profile.Id, new[] { action });

    var storedProfile = await profiles.GetLaunchProfileAsync(profile.Id);
    var storedActions = await profiles.GetLaunchActionsAsync(profile.Id);

    Assert.NotNull(storedProfile);
    Assert.Equal("Streaming", storedProfile!.Name);
    Assert.Equal("-dx12", storedProfile.GameArgumentsOverride);
    Assert.Equal(1, storedActions.Count);
    Assert.Equal("OBS", storedActions[0].Name);
    Assert.True(storedActions[0].CloseWithGame);
}

static async Task LaunchProfileServiceKeepsOneDefault()
{
    using var temp = new TempDirectory();
    var dbPath = Path.Combine(temp.Path, "launcher.db");
    var games = new SqliteGameRepository(dbPath);
    var profileRepository = new SqliteLaunchProfileRepository(dbPath);
    var service = new LaunchProfileService(profileRepository);

    var now = DateTimeOffset.UtcNow;
    var firstGame = new Game(Guid.NewGuid(), "Control", now, now);
    var secondGame = new Game(Guid.NewGuid(), "CONTROL [PC]", now, now);
    await games.UpsertGameAsync(firstGame);
    await games.UpsertGameAsync(secondGame);

    var firstInstall = new GameInstallation(
        Guid.NewGuid(), firstGame.Id, GameSource.Steam, "870780",
        @"C:\Games\ControlSteam", @"C:\Games\ControlSteam\Control.exe", null, true);
    var secondInstall = new GameInstallation(
        Guid.NewGuid(), secondGame.Id, GameSource.Epic, "control-epic",
        @"C:\Games\ControlEpic", @"C:\Games\ControlEpic\Control.exe", null, true);

    await games.UpsertInstallationAsync(firstInstall);
    await games.UpsertInstallationAsync(secondInstall);

    var item = new GameLibraryItem(
        firstGame,
        new[] { firstInstall, secondInstall },
        0,
        null);

    var firstProfile = new LaunchProfile(
        Guid.NewGuid(), firstGame.Id, "Steam Setup", firstInstall.Id,
        null, true, now, now);
    await service.SaveAsync(item, firstProfile, Array.Empty<LaunchAction>());

    var secondProfile = new LaunchProfile(
        Guid.NewGuid(), secondGame.Id, "Epic Setup", secondInstall.Id,
        null, true, now, now);
    await service.SaveAsync(item, secondProfile, Array.Empty<LaunchAction>());

    var stored = await service.GetProfilesAsync(item);
    Assert.Equal(2, stored.Count);
    Assert.Equal(1, stored.Count(x => x.Profile.IsDefault));
    Assert.Equal(secondProfile.Id, stored.Single(x => x.Profile.IsDefault).Profile.Id);
}

static async Task SmartLaunchOrdersActionsAndCleansCompanions()
{
    using var temp = new TempDirectory();
    var dbPath = Path.Combine(temp.Path, "launcher.db");
    var games = new SqliteGameRepository(dbPath);
    var profileRepository = new SqliteLaunchProfileRepository(dbPath);
    var profileService = new LaunchProfileService(profileRepository);

    var events = new List<string>();
    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Smart Launch Test", now, now);
    var installation = new GameInstallation(
        Guid.NewGuid(),
        game.Id,
        GameSource.Steam,
        "12345",
        @"C:\Games\Smart",
        @"C:\Games\Smart\Smart.exe",
        "steam://rungameid/12345",
        true);

    await games.UpsertGameAsync(game);
    await games.UpsertInstallationAsync(installation);

    var item = new GameLibraryItem(game, new[] { installation }, 0, null);
    var profile = new LaunchProfile(
        Guid.NewGuid(),
        game.Id,
        "Full Setup",
        installation.Id,
        "-dx12",
        true,
        now,
        now);

    var actions = new[]
    {
        new LaunchAction(
            Guid.NewGuid(), profile.Id, LaunchActionStage.PreLaunch,
            "Prepare", @"C:\Tools\prepare.exe", null, null,
            0, true, false, true),
        new LaunchAction(
            Guid.NewGuid(), profile.Id, LaunchActionStage.Companion,
            "Monitor", @"C:\Tools\monitor.exe", null, null,
            1, false, true, true),
        new LaunchAction(
            Guid.NewGuid(), profile.Id, LaunchActionStage.PostGame,
            "Cleanup", @"C:\Tools\cleanup.exe", null, null,
            2, true, false, true)
    };

    await profileService.SaveAsync(item, profile, actions);

    var gameRuntime = new RecordingGameRuntime(events, now, now.AddSeconds(30));
    var sessions = new GameSessionService(games, gameRuntime);
    var external = new RecordingExternalRuntime(events);
    var smart = new SmartLaunchService(profileService, sessions, external);

    await smart.LaunchAsync(item);

    Assert.SequenceEqual(
        new[]
        {
            "start:Prepare",
            "wait:Prepare",
            "start:Monitor",
            "game:start",
            "game:wait",
            "stop:Monitor",
            "start:Cleanup",
            "wait:Cleanup"
        },
        events);

    Assert.NotNull(gameRuntime.LastInstallation);
    Assert.Equal("-dx12", gameRuntime.LastInstallation!.LaunchArguments);
    Assert.Equal<string?>(null, gameRuntime.LastInstallation.LaunchUri);
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
    private readonly DiscoveredGame _game;

    private FixedAdapter(string id, GameSource source, string externalId, string title)
    {
        Id = id;
        Source = source;
        DisplayName = id;

        var gameId = StableId.FromText($"{id}-game:{externalId}");
        var installId = StableId.FromText($"{id}-install:{externalId}");
        var now = DateTimeOffset.UtcNow;

        _game = new DiscoveredGame(
            new Game(gameId, title, now, now),
            new GameInstallation(
                installId, gameId, source, externalId,
                $@"C:\Games\{id}\{externalId}",
                null,
                source == GameSource.Steam ? $"steam://rungameid/{externalId}" : null,
                true));
    }

    public string Id { get; }
    public string DisplayName { get; }
    public GameSource Source { get; }

    public static FixedAdapter Create(string id, GameSource source, string externalId, string title) =>
        new(id, source, externalId, title);

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DiscoveredGame>>(new[] { _game });
}

file sealed class MutableAdapter : IGameSourceAdapter
{
    private readonly string _externalId;
    private readonly string _title;

    public MutableAdapter(string id, GameSource source, string externalId, string title)
    {
        Id = id;
        DisplayName = id;
        Source = source;
        _externalId = externalId;
        _title = title;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public GameSource Source { get; }
    public bool IsInstalled { get; set; } = true;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsInstalled)
        {
            return Task.FromResult<IReadOnlyList<DiscoveredGame>>(
                Array.Empty<DiscoveredGame>());
        }

        var gameId = StableId.FromText($"{Id}-game:{_externalId}");
        var installId = StableId.FromText($"{Id}-install:{_externalId}");
        var now = DateTimeOffset.UtcNow;

        IReadOnlyList<DiscoveredGame> result =
        [
            new DiscoveredGame(
                new Game(gameId, _title, now, now),
                new GameInstallation(
                    installId,
                    gameId,
                    Source,
                    _externalId,
                    $@"C:\Games\{Id}\{_externalId}",
                    null,
                    null,
                    true))
        ];

        return Task.FromResult(result);
    }
}

file sealed class StaticHttpHandler : HttpMessageHandler
{
    private readonly byte[] _content;

    public StaticHttpHandler(byte[] content)
    {
        _content = content;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_content)
        };

        return Task.FromResult(response);
    }
}

file sealed class RecordingGameRuntime : IGameRuntime
{
    private readonly List<string> _events;
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _end;

    public RecordingGameRuntime(
        List<string> events,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _events = events;
        _start = start;
        _end = end;
    }

    public GameInstallation? LastInstallation { get; private set; }

    public Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default)
    {
        LastInstallation = installation;
        _events.Add("game:start");
        return Task.FromResult<IGameRunHandle>(
            new RecordingGameRunHandle(_events, _start, _end, installation.ExecutablePath));
    }
}

file sealed class RecordingGameRunHandle : IGameRunHandle
{
    private readonly List<string> _events;
    private readonly DateTimeOffset _end;

    public RecordingGameRunHandle(
        List<string> events,
        DateTimeOffset start,
        DateTimeOffset end,
        string? executablePath)
    {
        _events = events;
        StartedUtc = start;
        _end = end;
        DetectedExecutablePath = executablePath;
    }

    public DateTimeOffset StartedUtc { get; }
    public string? DetectedExecutablePath { get; }

    public Task<DateTimeOffset> WaitForExitAsync(
        CancellationToken cancellationToken = default)
    {
        _events.Add("game:wait");
        return Task.FromResult(_end);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

file sealed class RecordingExternalRuntime : IExternalProgramRuntime
{
    private readonly List<string> _events;

    public RecordingExternalRuntime(List<string> events)
    {
        _events = events;
    }

    public Task<IExternalProgramHandle> StartAsync(
        LaunchAction action,
        CancellationToken cancellationToken = default)
    {
        _events.Add($"start:{action.Name}");
        return Task.FromResult<IExternalProgramHandle>(
            new RecordingExternalHandle(_events, action.Name));
    }
}

file sealed class RecordingExternalHandle : IExternalProgramHandle
{
    private readonly List<string> _events;
    private readonly string _name;

    public RecordingExternalHandle(List<string> events, string name)
    {
        _events = events;
        _name = name;
    }

    public bool IsRunning { get; private set; } = true;

    public Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        _events.Add($"wait:{_name}");
        IsRunning = false;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _events.Add($"stop:{_name}");
        IsRunning = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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

    public Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IGameRunHandle>(
            new FakeRunHandle(_start, _end, installation.ExecutablePath));
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

    public Task<DateTimeOffset> WaitForExitAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_end);

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

    public static void SequenceEqual<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"Expected sequence '{string.Join(", ", expected)}', got '{string.Join(", ", actual)}'.");
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
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GameLauncherTests",
            Guid.NewGuid().ToString("N"));
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
