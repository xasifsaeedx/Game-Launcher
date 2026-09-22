namespace GameLauncher.Tests;

internal static class DiscoveryTests
{
    internal static Task SteamManifestParserReadsFields()
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

    internal static Task SteamLibraryParserReadsPaths()
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

    internal static Task EpicManifestParserReadsFields()
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

    internal static async Task EpicAdapterDiscoversInstallation()
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

    internal static async Task XboxAdapterDiscoversInstallation()
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

    internal static async Task LibraryServiceAddsManualGame()
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

    internal static async Task SourceSyncRemainsIdempotent()
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

    internal static async Task SourceRescanRetiresRemovedInstallations()
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

    internal static async Task UnifiedLibraryMergesExactTitles()
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

    internal static async Task SteamArtworkEnricherCachesCover()
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
}
