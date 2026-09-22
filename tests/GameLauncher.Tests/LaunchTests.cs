namespace GameLauncher.Tests;

internal static class LaunchTests
{
    internal static async Task LaunchProfileRepositoryPersistsProfileAndActions()
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

    internal static async Task LaunchProfileServiceKeepsOneDefault()
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

    internal static async Task SmartLaunchOrdersActionsAndCleansCompanions()
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

    internal static async Task SessionServicePersistsPlaytime()
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
}
