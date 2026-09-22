namespace GameLauncher.Tests;

internal static class FoundationTests
{
    internal static Task AppPathsCreatesExpectedFolders()
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

    internal static Task StableIdsAreDeterministic()
    {
        var first = StableId.FromText("steam-game:205100");
        var second = StableId.FromText("steam-game:205100");
        var other = StableId.FromText("steam-game:292030");

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        return Task.CompletedTask;
    }

    internal static Task TitleNormalizationIsConservative()
    {
        Assert.Equal("control", GameTitleNormalizer.Normalize("Control™"));
        Assert.Equal("control", GameTitleNormalizer.Normalize("CONTROL [PC]"));
        Assert.NotEqual(
            GameTitleNormalizer.Normalize("Control"),
            GameTitleNormalizer.Normalize("Control Ultimate Edition"));
        return Task.CompletedTask;
    }
}
