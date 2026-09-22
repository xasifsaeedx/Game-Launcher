namespace GameLauncher.Tests;

internal static class RepositoryTests
{
    internal static async Task RepositoryRoundTripsFields()
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

    internal static async Task RepositoryTotalsPlaySessions()
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
}
