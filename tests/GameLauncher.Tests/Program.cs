using System.Net;
using System.Net.Http;
using System.Xml.Linq;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Runtime;
using GameLauncher.Core.Security;
using GameLauncher.Core.Services;
using GameLauncher.Core.Sync;
using GameLauncher.Core.Utilities;
using GameLauncher.Infrastructure.Adapters;
using GameLauncher.Infrastructure.Graphics;
using GameLauncher.Infrastructure.Metadata;
using GameLauncher.Infrastructure.Repositories;
using GameLauncher.Infrastructure.Storage;
using GameLauncher.Infrastructure.Sync;
using Microsoft.Data.Sqlite;

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
    ("Overlay formatter emits requested telemetry", OverlayFormatterEmitsRequestedTelemetry),
    ("Overlay settings persist and normalize", OverlaySettingsPersistAndNormalize),
    ("Overlay service skips disabled overlay", OverlayServiceSkipsDisabledOverlay),
    ("Session service owns overlay lifecycle", SessionServiceOwnsOverlayLifecycle),
    ("Hatchable settings require HTTPS", HatchableSettingsRequireHttps),
    ("Hatchable matching prefers Steam app ID", HatchableMatchingPrefersSteamId),
    ("Hatchable matching falls back to exact normalized title", HatchableMatchingFallsBackToTitle),
    ("Hatchable settings are protected at rest", HatchableSettingsAreProtectedAtRest),
    ("Hatchable API client parses and pushes sync data", HatchableApiClientParsesAndPushes),
    ("Hatchable sync pushes local playtime as playing", HatchableSyncPushesLocalPlaytime),
    ("Graphics tier recognizes GTX 1660 Super", GraphicsTierRecognizesGtx1660Super),
    ("Graphics recommendation targets 1080p quality safely", GraphicsRecommendationTargetsQuality),
    ("Graphics catalog matches Steam ID before title", GraphicsCatalogMatchesSteamId),
    ("Graphics optimizer settings persist", GraphicsOptimizerSettingsPersist),
    ("Safe INI graphics patch backs up and restores", SafeIniGraphicsPatchBacksUpAndRestores),
    ("Safe XML graphics patch changes existing fields only", SafeXmlGraphicsPatchChangesExistingFieldsOnly),
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
    Console.WriteLine($"All {tests.Length} Phase 6 tests passed.");
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

static Task OverlayFormatterEmitsRequestedTelemetry()
{
    var settings = new OverlaySettings(true, true, true, true, 500);
    var text = OverlayTextFormatter.Format(
        settings,
        new OverlayMetrics(59.6, 73.2, 64.8));

    Assert.Equal("FPS 60  |  GPU 73%  |  TEMP 65C", text);

    var gpuOnly = OverlayTextFormatter.Format(
        settings with { ShowFps = false, ShowGpuTemperature = false },
        new OverlayMetrics(120, 51.4, 70));

    Assert.Equal("GPU 51%", gpuOnly);
    return Task.CompletedTask;
}

static async Task OverlaySettingsPersistAndNormalize()
{
    using var temp = new TempDirectory();
    var repository = new SqliteOverlaySettingsRepository(
        Path.Combine(temp.Path, "launcher.db"));

    await repository.SaveAsync(
        new OverlaySettings(true, true, false, true, 50));

    var stored = await repository.GetAsync();

    Assert.True(stored.Enabled);
    Assert.True(stored.ShowFps);
    Assert.True(!stored.ShowGpuUsage);
    Assert.True(stored.ShowGpuTemperature);
    Assert.Equal(250, stored.UpdateIntervalMs);
}

static async Task OverlayServiceSkipsDisabledOverlay()
{
    var settings = new MemoryOverlaySettingsRepository(
        new OverlaySettings(false, true, true, true, 500));
    var runtime = new RecordingOverlayRuntime(new List<string>());
    var service = new GameplayOverlayService(settings, runtime);

    await using var session = await service.StartForGameAsync(1234);

    Assert.Equal(0, runtime.StartCount);
}

static async Task SessionServiceOwnsOverlayLifecycle()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));
    var events = new List<string>();
    var now = DateTimeOffset.UtcNow;

    var game = new Game(Guid.NewGuid(), "Overlay Lifecycle", now, now);
    var installation = new GameInstallation(
        Guid.NewGuid(),
        game.Id,
        GameSource.Manual,
        "overlay-lifecycle",
        temp.Path,
        Path.Combine(temp.Path, "Game.exe"),
        null,
        true);

    await repository.UpsertGameAsync(game);
    await repository.UpsertInstallationAsync(installation);

    var settings = new MemoryOverlaySettingsRepository(OverlaySettings.Default);
    var overlayRuntime = new RecordingOverlayRuntime(events);
    var overlay = new GameplayOverlayService(settings, overlayRuntime);
    var runtime = new OverlayAwareGameRuntime(events, now, now.AddSeconds(10));
    var sessions = new GameSessionService(repository, runtime, overlay);

    await sessions.LaunchAndTrackAsync(installation);

    Assert.SequenceEqual(
        new[]
        {
            "game:start",
            "overlay:start:4242",
            "game:wait",
            "overlay:dispose"
        },
        events);
}

static Task HatchableSettingsRequireHttps()
{
    Assert.True(
        new HatchableSyncSettings(
            "https://example.hatchable.site",
            "gl_test",
            true).IsConfigured);

    Assert.True(
        !new HatchableSyncSettings(
            "http://example.hatchable.site",
            "gl_test",
            true).IsConfigured);

    return Task.CompletedTask;
}

static Task HatchableMatchingPrefersSteamId()
{
    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Control Ultimate Edition", now, now);
    var install = new GameInstallation(
        Guid.NewGuid(), game.Id, GameSource.Steam, "870780",
        @"C:\Games\Control", @"C:\Games\Control\Control.exe",
        "steam://rungameid/870780", true);
    var local = new GameLibraryItem(game, new[] { install }, 0, null);

    HatchableRemoteGame[] remote =
    [
        new(4, "Control: Ultimate Edition", "PC, PS5", 870780, null, 4, "next", null, null, 0, null),
        new(5, "Control Ultimate Edition", "PC", 999999, null, 5, null, null, null, 0, null)
    ];

    var match = HatchableSyncService.FindMatch(local, remote);
    Assert.NotNull(match);
    Assert.Equal(4, match!.RemoteGameId);
    return Task.CompletedTask;
}

static Task HatchableMatchingFallsBackToTitle()
{
    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "CONTROL [PC]", now, now);
    var install = new GameInstallation(
        Guid.NewGuid(), game.Id, GameSource.Epic, "control-epic",
        @"C:\Games\Control", @"C:\Games\Control\Control.exe", null, true);
    var local = new GameLibraryItem(game, new[] { install }, 0, null);

    HatchableRemoteGame[] remote =
    [
        new(4, "Control™", "PC", null, null, 4, "next", null, null, 0, null)
    ];

    var match = HatchableSyncService.FindMatch(local, remote);
    Assert.NotNull(match);
    Assert.Equal(4, match!.RemoteGameId);
    return Task.CompletedTask;
}

static async Task HatchableSettingsAreProtectedAtRest()
{
    using var temp = new TempDirectory();
    var path = Path.Combine(temp.Path, "launcher.db");
    var protector = new PrefixSecretProtector();
    var repository = new SqliteHatchableSyncRepository(path, protector);

    await repository.SaveSettingsAsync(
        new HatchableSyncSettings(
            "https://example.hatchable.site/",
            "gl_secret_token",
            true));

    var roundTrip = await repository.GetSettingsAsync();
    Assert.NotNull(roundTrip);
    Assert.Equal("https://example.hatchable.site", roundTrip!.BaseUrl);
    Assert.Equal("gl_secret_token", roundTrip.Token);
    Assert.True(roundTrip.AutoSync);

    await using var connection = new SqliteConnection($"Data Source={path}");
    await connection.OpenAsync();
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT protected_token FROM hatchable_sync_settings WHERE id=1;";
    var stored = Convert.ToString(await command.ExecuteScalarAsync());

    Assert.Equal("protected::gl_secret_token", stored);
    Assert.NotEqual("gl_secret_token", stored);
}

static async Task HatchableApiClientParsesAndPushes()
{
    var handler = new HatchableHttpHandler();
    using var http = new HttpClient(handler);
    var client = new HatchableApiClient(http);
    var settings = new HatchableSyncSettings(
        "https://my-game-library.hatchable.site",
        "gl_test",
        true);

    var games = await client.GetGamesAsync(settings);
    Assert.Equal(1, games.Count);
    Assert.Equal(2, games[0].RemoteGameId);
    Assert.Equal("Crimson Desert", games[0].Title);
    Assert.Equal(3321460L, games[0].SteamAppId);
    Assert.Equal("next", games[0].LibraryStatus);
    Assert.Equal("playing", games[0].ProgressStatus);
    Assert.Equal(7200L, games[0].PlaytimeSeconds);

    var updated = await client.PushGamesAsync(
        settings,
        new[]
        {
            new HatchableGamePush(
                2,
                9000,
                DateTimeOffset.Parse("2026-09-21T10:00:00Z"),
                "playing",
                9)
        });

    Assert.Equal(1, updated);
    Assert.Equal("Bearer gl_test", handler.LastAuthorization);
    Assert.True(handler.LastPostBody?.Contains("\"remote_game_id\":2", StringComparison.Ordinal) == true);
    Assert.True(handler.LastPostBody?.Contains("\"playtime_seconds\":9000", StringComparison.Ordinal) == true);
    Assert.True(handler.LastPostBody?.Contains("\"rating\":9", StringComparison.Ordinal) == true);
}

static async Task HatchableSyncPushesLocalPlaytime()
{
    using var temp = new TempDirectory();
    var path = Path.Combine(temp.Path, "launcher.db");
    var gamesRepository = new SqliteGameRepository(path);
    var now = DateTimeOffset.UtcNow;

    var game = new Game(Guid.NewGuid(), "Different Store Title", now, now);
    var install = new GameInstallation(
        Guid.NewGuid(), game.Id, GameSource.Steam, "870780",
        @"C:\Games\Control", @"C:\Games\Control\Control.exe",
        "steam://rungameid/870780", true);

    await gamesRepository.UpsertGameAsync(game);
    await gamesRepository.UpsertInstallationAsync(install);

    var session = new PlaySession(Guid.NewGuid(), game.Id, now.AddMinutes(-10), null, null);
    await gamesRepository.AddPlaySessionAsync(session);
    await gamesRepository.EndPlaySessionAsync(session.Id, now, 600);

    var library = new GameLibraryService(
        gamesRepository,
        Array.Empty<IGameSourceAdapter>());

    var remote = new HatchableRemoteGame(
        4,
        "Control: Ultimate Edition",
        "PC, PS5",
        870780,
        null,
        4,
        "next",
        null,
        null,
        0,
        null);

    var syncRepository = new MemoryHatchableSyncRepository(
        new HatchableSyncSettings("https://example.test", "gl_test", true),
        new[] { remote });
    var api = new RecordingHatchableApiClient(new[] { remote });
    var service = new HatchableSyncService(syncRepository, api, library);

    var result = await service.SyncAsync();

    Assert.Equal(1, result.MatchedCount);
    Assert.Equal(1, result.PushedCount);
    Assert.Equal(1, api.LastPush.Count);
    Assert.Equal(4, api.LastPush[0].RemoteGameId);
    Assert.Equal(600L, api.LastPush[0].PlaytimeSeconds);
    Assert.Equal("playing", api.LastPush[0].ProgressStatus);
    Assert.NotNull(api.LastPush[0].LastPlayedAt);
}

static Task GraphicsTierRecognizesGtx1660Super()
{
    Assert.Equal(
        GraphicsPerformanceTier.Mainstream1080p,
        HardwareTierClassifier.ClassifyGpuName("EVGA NVIDIA GeForce GTX 1660 SUPER"));

    Assert.Equal(
        GraphicsPerformanceTier.Strong1080p,
        HardwareTierClassifier.ClassifyGpuName("Intel Arc B580"));

    Assert.Equal(
        GraphicsPerformanceTier.Unknown,
        HardwareTierClassifier.ClassifyGpuName("Future Mystery GPU"));

    Assert.Equal(
        "Manual",
        GraphicsRecommendationPolicy.RecommendPreset(
            GraphicsPerformanceTier.Unknown,
            GraphicsQualityPreference.Quality));

    return Task.CompletedTask;
}

static Task GraphicsRecommendationTargetsQuality()
{
    var target = GraphicsOptimizerSettings.Default;
    Assert.Equal(1920, target.TargetWidth);
    Assert.Equal(1080, target.TargetHeight);
    Assert.Equal(60, target.TargetFps);
    Assert.Equal(GraphicsQualityPreference.Quality, target.QualityPreference);

    Assert.Equal(
        "High",
        GraphicsRecommendationPolicy.RecommendPreset(
            GraphicsPerformanceTier.Mainstream1080p,
            GraphicsQualityPreference.Quality));

    var tips = GraphicsRecommendationPolicy.BuildGeneralTips(
        GraphicsPerformanceTier.Mainstream1080p,
        target);

    Assert.True(tips.Any(x => x.Contains("Ray tracing: Off", StringComparison.Ordinal)));
    Assert.True(tips.Any(x => x.Contains("Textures: High", StringComparison.Ordinal)));
    return Task.CompletedTask;
}

static Task GraphicsCatalogMatchesSteamId()
{
    var catalog = new GraphicsProfileCatalog(BuiltInGraphicsProfiles.Create());
    var now = DateTimeOffset.UtcNow;
    var game = new Game(Guid.NewGuid(), "Totally Different Local Title", now, now);
    var install = new GameInstallation(
        Guid.NewGuid(),
        game.Id,
        GameSource.Steam,
        "292030",
        @"C:\Games\Witcher3",
        @"C:\Games\Witcher3\witcher3.exe",
        "steam://rungameid/292030",
        true);

    var match = catalog.FindMatch(
        new GameLibraryItem(game, new[] { install }, 0, null));

    Assert.NotNull(match);
    Assert.Equal("witcher3", match!.Id);
    Assert.True(match.SupportsSafeApply);
    return Task.CompletedTask;
}

static async Task GraphicsOptimizerSettingsPersist()
{
    using var temp = new TempDirectory();
    var repository = new SqliteGraphicsOptimizerRepository(
        Path.Combine(temp.Path, "launcher.db"));

    await repository.SaveSettingsAsync(
        new GraphicsOptimizerSettings(
            2560,
            1440,
            144,
            GraphicsQualityPreference.Balanced,
            GraphicsPerformanceTier.HighEnd,
            true));

    var stored = await repository.GetSettingsAsync();
    Assert.Equal(2560, stored.TargetWidth);
    Assert.Equal(1440, stored.TargetHeight);
    Assert.Equal(144, stored.TargetFps);
    Assert.Equal(GraphicsQualityPreference.Balanced, stored.QualityPreference);
    Assert.Equal<GraphicsPerformanceTier?>(GraphicsPerformanceTier.HighEnd, stored.TierOverride);
    Assert.True(stored.AutoApplyBeforeLaunch);
}

static async Task SafeIniGraphicsPatchBacksUpAndRestores()
{
    using var temp = new TempDirectory();
    var path = Path.Combine(temp.Path, "user.settings");
    const string original =
        "Resolution=1280x720\n" +
        "LimitFPS=30\n" +
        "TextureQuality=High\n";
    await File.WriteAllTextAsync(path, original);

    var profile = new GameGraphicsProfile(
        "test-ini",
        "Test INI",
        null,
        Array.Empty<string>(),
        path,
        GraphicsConfigFormat.IniKeyValue,
        new[]
        {
            new SafeGraphicsPatch("Resolution", "Resolution", "{width}x{height}"),
            new SafeGraphicsPatch("FPS limit", "LimitFPS", "{fps}"),
            new SafeGraphicsPatch("Missing", "ThisKeyDoesNotExist", "1")
        },
        Array.Empty<string>());

    var hardware = new HardwareProfile(
        "CPU",
        "GTX 1660 SUPER",
        32,
        1920,
        1080,
        60,
        GraphicsPerformanceTier.Mainstream1080p,
        DateTimeOffset.UtcNow);

    var target = GraphicsOptimizerSettings.Default;
    var recommendation = new GraphicsRecommendation(
        "Test INI",
        "High",
        GraphicsPerformanceTier.Mainstream1080p,
        target,
        profile,
        path,
        Array.Empty<string>(),
        profile.SafePatches);

    var runtime = new SafeGraphicsConfigRuntime();
    var applied = await runtime.ApplyAsync(recommendation, hardware);

    Assert.True(applied.Changed);
    Assert.True(File.Exists(path + ".game-launcher.bak"));
    Assert.Equal(original, await File.ReadAllTextAsync(path + ".game-launcher.bak"));

    var updated = await File.ReadAllTextAsync(path);
    Assert.True(updated.Contains("Resolution=1920x1080", StringComparison.Ordinal));
    Assert.True(updated.Contains("LimitFPS=60", StringComparison.Ordinal));
    Assert.True(updated.Contains("TextureQuality=High", StringComparison.Ordinal));
    Assert.True(!updated.Contains("ThisKeyDoesNotExist", StringComparison.Ordinal));
    Assert.True(applied.Warnings.Any(x => x.Contains("not present", StringComparison.Ordinal)));

    var secondTarget = target with { TargetWidth = 1600, TargetHeight = 900 };
    var secondRecommendation = recommendation with { Target = secondTarget };
    await runtime.ApplyAsync(secondRecommendation, hardware);
    Assert.Equal(original, await File.ReadAllTextAsync(path + ".game-launcher.bak"));

    var restored = await runtime.RestoreAsync(recommendation);
    Assert.True(restored.Changed);
    Assert.Equal(original, await File.ReadAllTextAsync(path));
}

static async Task SafeXmlGraphicsPatchChangesExistingFieldsOnly()
{
    using var temp = new TempDirectory();
    var path = Path.Combine(temp.Path, "settings.xml");
    const string original =
        "<Settings><video>" +
        "<ScreenWidth value=\"1280\" />" +
        "<ScreenHeight value=\"720\" />" +
        "<RefreshRate value=\"60\" />" +
        "<VSync value=\"0\" />" +
        "<TextureQuality value=\"2\" />" +
        "</video></Settings>";
    await File.WriteAllTextAsync(path, original);

    var profile = new GameGraphicsProfile(
        "test-xml",
        "Test XML",
        null,
        Array.Empty<string>(),
        path,
        GraphicsConfigFormat.XmlValueAttribute,
        new[]
        {
            new SafeGraphicsPatch("Width", "ScreenWidth", "{width}"),
            new SafeGraphicsPatch("Height", "ScreenHeight", "{height}"),
            new SafeGraphicsPatch("VSync", "VSync", "{vsync}"),
            new SafeGraphicsPatch("Missing", "UnknownSetting", "1")
        },
        Array.Empty<string>());

    var hardware = new HardwareProfile(
        "CPU",
        "GTX 1660 SUPER",
        32,
        1920,
        1080,
        60,
        GraphicsPerformanceTier.Mainstream1080p,
        DateTimeOffset.UtcNow);

    var recommendation = new GraphicsRecommendation(
        "Test XML",
        "High",
        GraphicsPerformanceTier.Mainstream1080p,
        GraphicsOptimizerSettings.Default,
        profile,
        path,
        Array.Empty<string>(),
        profile.SafePatches);

    var runtime = new SafeGraphicsConfigRuntime();
    var applied = await runtime.ApplyAsync(recommendation, hardware);
    var updated = await File.ReadAllTextAsync(path);
    _ = XDocument.Parse(updated);

    Assert.True(applied.Changed);
    Assert.True(updated.Contains("ScreenWidth value=\"1920\"", StringComparison.Ordinal));
    Assert.True(updated.Contains("ScreenHeight value=\"1080\"", StringComparison.Ordinal));
    Assert.True(updated.Contains("VSync value=\"1\"", StringComparison.Ordinal));
    Assert.True(updated.Contains("TextureQuality value=\"2\"", StringComparison.Ordinal));
    Assert.True(!updated.Contains("UnknownSetting", StringComparison.Ordinal));
    Assert.True(applied.Warnings.Any(x => x.Contains("not present", StringComparison.Ordinal)));
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
    public int? ProcessId => 1001;
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

file sealed class MemoryOverlaySettingsRepository : IOverlaySettingsRepository
{
    private OverlaySettings _settings;

    public MemoryOverlaySettingsRepository(OverlaySettings settings)
    {
        _settings = settings;
    }

    public Task<OverlaySettings> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings);

    public Task SaveAsync(
        OverlaySettings settings,
        CancellationToken cancellationToken = default)
    {
        _settings = settings.Normalize();
        return Task.CompletedTask;
    }
}

file sealed class RecordingOverlayRuntime : IGameplayOverlayRuntime
{
    private readonly List<string> _events;

    public RecordingOverlayRuntime(List<string> events)
    {
        _events = events;
    }

    public int StartCount { get; private set; }

    public Task<IOverlaySession> StartAsync(
        int processId,
        OverlaySettings settings,
        CancellationToken cancellationToken = default)
    {
        StartCount++;
        _events.Add($"overlay:start:{processId}");
        return Task.FromResult<IOverlaySession>(
            new RecordingOverlaySession(_events));
    }

    public Task<OverlayRuntimeStatus> GetStatusAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new OverlayRuntimeStatus(true, "ready"));
}

file sealed class RecordingOverlaySession : IOverlaySession
{
    private readonly List<string> _events;

    public RecordingOverlaySession(List<string> events)
    {
        _events = events;
    }

    public ValueTask DisposeAsync()
    {
        _events.Add("overlay:dispose");
        return ValueTask.CompletedTask;
    }
}

file sealed class OverlayAwareGameRuntime : IGameRuntime
{
    private readonly List<string> _events;
    private readonly DateTimeOffset _start;
    private readonly DateTimeOffset _end;

    public OverlayAwareGameRuntime(
        List<string> events,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _events = events;
        _start = start;
        _end = end;
    }

    public Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default)
    {
        _events.Add("game:start");
        return Task.FromResult<IGameRunHandle>(
            new OverlayAwareGameRunHandle(_events, _start, _end));
    }
}

file sealed class OverlayAwareGameRunHandle : IGameRunHandle
{
    private readonly List<string> _events;
    private readonly DateTimeOffset _end;

    public OverlayAwareGameRunHandle(
        List<string> events,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        _events = events;
        StartedUtc = start;
        _end = end;
    }

    public DateTimeOffset StartedUtc { get; }
    public int? ProcessId => 4242;
    public string? DetectedExecutablePath => @"C:\Games\Overlay\Game.exe";

    public Task<DateTimeOffset> WaitForExitAsync(
        CancellationToken cancellationToken = default)
    {
        _events.Add("game:wait");
        return Task.FromResult(_end);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

file sealed class PrefixSecretProtector : ISecretProtector
{
    public string Protect(string plaintext) => "protected::" + plaintext;

    public string Unprotect(string protectedValue) =>
        protectedValue.StartsWith("protected::", StringComparison.Ordinal)
            ? protectedValue["protected::".Length..]
            : throw new InvalidOperationException("Invalid protected value.");
}

file sealed class HatchableHttpHandler : HttpMessageHandler
{
    public string? LastAuthorization { get; private set; }
    public string? LastPostBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastAuthorization = request.Headers.Authorization?.ToString();

        if (request.Method == HttpMethod.Get)
        {
            return JsonResponse(
                """
                {
                  "version": 1,
                  "games": [
                    {
                      "id": 2,
                      "title": "Crimson Desert",
                      "platforms": "PC, PS5",
                      "steam_app_id": 3321460,
                      "cover_url": null,
                      "rank_score": 2,
                      "status": "next",
                      "progress_status": "playing",
                      "rating": 8,
                      "playtime_seconds": 7200,
                      "last_played_at": "2026-09-21T09:00:00Z"
                    }
                  ]
                }
                """);
        }

        LastPostBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return JsonResponse("""{"ok":true,"updated":1}""");
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
}

file sealed class MemoryHatchableSyncRepository : IHatchableSyncRepository
{
    private HatchableSyncSettings? _settings;
    private IReadOnlyList<HatchableRemoteGame> _games;

    public MemoryHatchableSyncRepository(
        HatchableSyncSettings? settings,
        IReadOnlyList<HatchableRemoteGame> games)
    {
        _settings = settings;
        _games = games;
    }

    public Task<HatchableSyncSettings?> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_settings);

    public Task SaveSettingsAsync(
        HatchableSyncSettings settings,
        CancellationToken cancellationToken = default)
    {
        _settings = settings.Normalize();
        return Task.CompletedTask;
    }

    public Task ClearSettingsAsync(CancellationToken cancellationToken = default)
    {
        _settings = null;
        return Task.CompletedTask;
    }

    public Task ReplaceRemoteGamesAsync(
        IReadOnlyList<HatchableRemoteGame> games,
        CancellationToken cancellationToken = default)
    {
        _games = games;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HatchableRemoteGame>> GetRemoteGamesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_games);
}

file sealed class RecordingHatchableApiClient : IHatchableApiClient
{
    private IReadOnlyList<HatchableRemoteGame> _games;

    public RecordingHatchableApiClient(IReadOnlyList<HatchableRemoteGame> games)
    {
        _games = games;
    }

    public IReadOnlyList<HatchableGamePush> LastPush { get; private set; } =
        Array.Empty<HatchableGamePush>();

    public Task<IReadOnlyList<HatchableRemoteGame>> GetGamesAsync(
        HatchableSyncSettings settings,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_games);

    public Task<int> PushGamesAsync(
        HatchableSyncSettings settings,
        IReadOnlyList<HatchableGamePush> games,
        CancellationToken cancellationToken = default)
    {
        LastPush = games.ToArray();
        _games = _games.Select(remote =>
        {
            var push = games.FirstOrDefault(x => x.RemoteGameId == remote.RemoteGameId);
            return push is null
                ? remote
                : remote with
                {
                    ProgressStatus = push.ProgressStatus,
                    Rating = push.Rating,
                    PlaytimeSeconds = Math.Max(remote.PlaytimeSeconds, push.PlaytimeSeconds),
                    LastPlayedAt = push.LastPlayedAt ?? remote.LastPlayedAt
                };
        }).ToArray();

        return Task.FromResult(games.Count);
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
    public int? ProcessId => 1002;
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
