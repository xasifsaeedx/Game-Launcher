namespace GameLauncher.Tests;

internal static class PersonalLibraryTests
{
    internal static Task SettingsRequireGoogleSheetsHttps()
    {
        Assert.True(new PersonalLibrarySettings(
            "https://docs.google.com/spreadsheets/d/test/edit",
            true).IsConfigured);

        Assert.True(!new PersonalLibrarySettings(
            "http://docs.google.com/spreadsheets/d/test/edit",
            true).IsConfigured);

        Assert.True(!new PersonalLibrarySettings(
            "https://example.com/library",
            true).IsConfigured);

        return Task.CompletedTask;
    }

    internal static Task MatcherPrefersSteamId()
    {
        var local = CreateLibraryItem(
            title: "Different Local Name",
            steamAppId: "870780");

        PersonalLibraryGame[] personalGames =
        [
            new(
                2,
                "Control Ultimate Edition",
                "PC",
                870780,
                null,
                4,
                "next",
                null,
                9)
        ];

        var match = PersonalLibraryMatcher.FindMatch(
            local,
            personalGames);

        Assert.NotNull(match);
        Assert.Equal("Control Ultimate Edition", match!.Title);
        return Task.CompletedTask;
    }

    internal static Task MatcherFallsBackToTitle()
    {
        var local = CreateLibraryItem(
            title: "CONTROL [PC]",
            steamAppId: null);

        PersonalLibraryGame[] personalGames =
        [
            new(
                3,
                "Control",
                "PC",
                null,
                null,
                1,
                "next",
                "playing",
                null)
        ];

        var match = PersonalLibraryMatcher.FindMatch(
            local,
            personalGames);

        Assert.NotNull(match);
        Assert.Equal("Control", match!.Title);
        return Task.CompletedTask;
    }

    internal static Task GoogleSheetsUrlConvertsToCsvExport()
    {
        var uri = GoogleSheetsPersonalLibraryClient.BuildCsvUri(
            "https://docs.google.com/spreadsheets/d/abc123/edit#gid=456");

        Assert.Equal(
            "https://docs.google.com/spreadsheets/d/abc123/export?format=csv&gid=456",
            uri.ToString());

        return Task.CompletedTask;
    }

    internal static Task GoogleSheetsLibraryParserReadsFlexibleColumns()
    {
        const string csv =
            "Game Title,Platform,Steam App ID,Rank,Status,Progress,Rating\n" +
            "Control Ultimate Edition,PC,870780,4,next,playing,9\n" +
            "Ghost of Yotei,PS5,,2,next,,10\n";

        var games = GoogleSheetsPersonalLibraryClient.Parse(csv);

        Assert.Equal(2, games.Count);
        Assert.Equal("Ghost of Yotei", games[0].Title);
        Assert.Equal<int?>(2, games[0].Rank);
        Assert.Equal<int?>(10, games[0].SheetRating);
        Assert.Equal("Control Ultimate Edition", games[1].Title);
        Assert.Equal<long?>(870780L, games[1].SteamAppId);
        Assert.Equal("playing", games[1].ProgressStatus);

        return Task.CompletedTask;
    }

    internal static async Task GamePreferencesPersistFavorites()
    {
        using var temp = new TempDirectory();
        var repository = new SqliteGamePreferenceRepository(
            Path.Combine(temp.Path, "launcher.db"));

        await repository.SetFavoriteAsync("control", true);
        var first = await repository.GetAsync("control");

        Assert.NotNull(first);
        Assert.True(first!.IsFavorite);

        await repository.SetFavoriteAsync("control", false);
        var second = await repository.GetAsync("control");

        Assert.NotNull(second);
        Assert.True(!second!.IsFavorite);
        Assert.Equal(1, (await repository.GetAllAsync()).Count);
    }

    internal static async Task GamePreferencesPersistRatings()
    {
        using var temp = new TempDirectory();
        var repository = new SqliteGamePreferenceRepository(
            Path.Combine(temp.Path, "launcher.db"));

        await repository.SetFavoriteAsync("control", true);
        await repository.SetRatingAsync("control", 9);

        var rated = await repository.GetAsync("control");
        Assert.NotNull(rated);
        Assert.True(rated!.IsFavorite);
        Assert.Equal<int?>(9, rated.Rating);

        await repository.SetRatingAsync("control", null);
        var cleared = await repository.GetAsync("control");
        Assert.NotNull(cleared);
        Assert.True(cleared!.IsFavorite);
        Assert.Equal<int?>(null, cleared.Rating);
    }

    internal static async Task GamePreferenceSchemaMigratesRatings()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "launcher.db");

        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE game_preferences(
                    game_key TEXT PRIMARY KEY NOT NULL,
                    is_favorite INTEGER NOT NULL,
                    updated_utc TEXT NOT NULL
                );
                INSERT INTO game_preferences(game_key, is_favorite, updated_utc)
                VALUES('control', 1, '2026-09-21T00:00:00Z');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = new SqliteGamePreferenceRepository(path);
        await repository.SetRatingAsync("control", 8);

        var migrated = await repository.GetAsync("control");
        Assert.NotNull(migrated);
        Assert.True(migrated!.IsFavorite);
        Assert.Equal<int?>(8, migrated.Rating);
    }

    internal static Task LibraryPresentationPolicyHandlesSearchAndFilters()
    {
        var item = CreateLibraryItem(
            title: "Control Ultimate Edition",
            steamAppId: "870780");

        var personalGame = new PersonalLibraryGame(
            4,
            "Control Ultimate Edition",
            "PC, PS5",
            870780,
            null,
            4,
            "next",
            "playing",
            9);

        Assert.True(LibraryPresentationPolicy.Matches(
            item,
            personalGame,
            false,
            "steam",
            LibraryFilterMode.All));

        Assert.True(LibraryPresentationPolicy.Matches(
            item,
            personalGame,
            true,
            "control",
            LibraryFilterMode.Favorites));

        Assert.True(LibraryPresentationPolicy.Matches(
            item,
            personalGame,
            false,
            null,
            LibraryFilterMode.NextUp));

        Assert.True(LibraryPresentationPolicy.Matches(
            item,
            personalGame,
            false,
            null,
            LibraryFilterMode.Playing));

        Assert.True(!LibraryPresentationPolicy.Matches(
            item,
            personalGame,
            false,
            "witcher",
            LibraryFilterMode.All));

        return Task.CompletedTask;
    }

    private static GameLibraryItem CreateLibraryItem(
        string title,
        string? steamAppId)
    {
        var now = DateTimeOffset.UtcNow;
        var game = new Game(
            Guid.NewGuid(),
            title,
            now,
            now);

        var installations = steamAppId is null
            ? Array.Empty<GameInstallation>()
            :
            [
                new GameInstallation(
                    Guid.NewGuid(),
                    game.Id,
                    GameSource.Steam,
                    steamAppId,
                    @"C:\Games\Control",
                    @"C:\Games\Control\Control.exe",
                    $"steam://rungameid/{steamAppId}",
                    true)
            ];

        return new GameLibraryItem(
            game,
            installations,
            0,
            null);
    }
}
