namespace GameLauncher.Tests;

internal static class HistoryTests
{
    internal static async Task SteamHistoryApiParsesOwnedGames()
    {
        const string json =
            """
            {
              "response": {
                "game_count": 1,
                "games": [
                  {
                    "appid": 1174180,
                    "name": "Red Dead Redemption 2",
                    "playtime_forever": 125,
                    "rtime_last_played": 1789992000
                  }
                ]
              }
            }
            """;

        using var http = new HttpClient(
            new StaticHttpHandler(Encoding.UTF8.GetBytes(json)));
        var client = new SteamHistoryApiClient(http);

        var games = await client.GetOwnedGamesAsync(
            new SteamHistorySettings(
                "76561198000000000",
                "test-api-key",
                true));

        Assert.Equal(1, games.Count);
        Assert.Equal(GameSource.Steam, games[0].Source);
        Assert.Equal("1174180", games[0].ExternalId);
        Assert.Equal("Red Dead Redemption 2", games[0].Title);
        Assert.Equal<long?>(7500L, games[0].PlaytimeSeconds);
        Assert.NotNull(games[0].LastPlayedUtc);
    }

    internal static async Task SteamHistoryCredentialsAreProtectedAtRest()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "launcher.db");
        var repository = new SqliteSteamHistorySettingsRepository(
            path,
            new PrefixSecretProtector());

        await repository.SaveAsync(
            new SteamHistorySettings(
                "76561198000000000",
                "very-secret-steam-key",
                true));

        var stored = await repository.GetAsync();
        Assert.NotNull(stored);
        Assert.Equal("very-secret-steam-key", stored!.ApiKey);
        Assert.True(stored.AutoSync);

        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT protected_api_key FROM steam_history_settings WHERE id=1;";
        var raw = Convert.ToString(await command.ExecuteScalarAsync());

        Assert.Equal("protected::very-secret-steam-key", raw);
        Assert.NotEqual("very-secret-steam-key", raw);
    }

    internal static async Task GenericCsvHistoryImporterReadsFlexibleColumns()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "gog.csv");
        await File.WriteAllTextAsync(
            path,
            "Game Title,Game ID,Platform,Hours Played,Last Played\n" +
            "The Witcher 3,witcher3,PC,12.5,2026-09-20T10:00:00Z\n");

        var parser = new GenericHistoryFileParser();
        var games = await parser.ParseAsync(GameSource.Gog, path);

        Assert.Equal(1, games.Count);
        Assert.Equal(GameSource.Gog, games[0].Source);
        Assert.Equal("The Witcher 3", games[0].Title);
        Assert.Equal("witcher3", games[0].ExternalId);
        Assert.Equal<long?>(45000L, games[0].PlaytimeSeconds);
        Assert.NotNull(games[0].LastPlayedUtc);
    }

    internal static async Task GenericJsonHistoryImporterReadsGamesArray()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "ubisoft.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "games": [
                {
                  "gameTitle": "Assassin's Creed Odyssey",
                  "productId": "ac-odyssey",
                  "platform": "PC",
                  "playtime_minutes": 90,
                  "lastPlayed": "2026-09-19T08:30:00Z"
                }
              ]
            }
            """);

        var parser = new GenericHistoryFileParser();
        var games = await parser.ParseAsync(GameSource.Ubisoft, path);

        Assert.Equal(1, games.Count);
        Assert.Equal("Assassin's Creed Odyssey", games[0].Title);
        Assert.Equal("ac-odyssey", games[0].ExternalId);
        Assert.Equal<long?>(5400L, games[0].PlaytimeSeconds);
    }

    internal static async Task GenericJsonHistoryImporterSkipsUnrelatedArrays()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "mixed.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "friends": [
                { "name": "Not A Game", "id": "friend-1" }
              ],
              "account": {
                "library": [
                  {
                    "gameTitle": "Dishonored",
                    "productId": "dishonored",
                    "hoursPlayed": 4
                  }
                ]
              }
            }
            """);

        var parser = new GenericHistoryFileParser();
        var games = await parser.ParseAsync(GameSource.EA, path);

        Assert.Equal(1, games.Count);
        Assert.Equal("Dishonored", games[0].Title);
        Assert.Equal("dishonored", games[0].ExternalId);
        Assert.Equal<long?>(14400L, games[0].PlaytimeSeconds);
    }

    internal static async Task PlayStationExcelHistoryImporterReadsGameWorksheet()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "playstation-data.xlsx");

        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Games Played");
            sheet.Cell(1, 1).Value = "Game Title";
            sheet.Cell(1, 2).Value = "Title ID";
            sheet.Cell(1, 3).Value = "Platform";
            sheet.Cell(1, 4).Value = "Hours Played";
            sheet.Cell(1, 5).Value = "Last Played";

            sheet.Cell(2, 1).Value = "Ghost of Tsushima";
            sheet.Cell(2, 2).Value = "PPSA02225";
            sheet.Cell(2, 3).Value = "PS5";
            sheet.Cell(2, 4).Value = 42.25;
            sheet.Cell(2, 5).Value = "2026-09-18T20:15:00Z";

            workbook.SaveAs(path);
        }

        var parser = new PlayStationExcelHistoryParser();
        var games = await parser.ParseAsync(GameSource.PlayStation, path);

        Assert.Equal(1, games.Count);
        Assert.Equal("Ghost of Tsushima", games[0].Title);
        Assert.Equal("PPSA02225", games[0].ExternalId);
        Assert.Equal("PS5", games[0].Platform);
        Assert.Equal<long?>(152100L, games[0].PlaytimeSeconds);
        Assert.NotNull(games[0].LastPlayedUtc);
    }

    internal static async Task HistoryRepositoryKeepsLargestSnapshot()
    {
        using var temp = new TempDirectory();
        var repository = new SqliteGameHistoryRepository(
            Path.Combine(temp.Path, "launcher.db"));

        var now = DateTimeOffset.UtcNow;
        var id = StableId.FromText("history:steam:123");

        await repository.UpsertAsync(
            new GameHistoryRecord(
                id,
                GameSource.Steam,
                "123",
                "Test Game",
                "PC",
                7200,
                now,
                now));

        await repository.UpsertAsync(
            new GameHistoryRecord(
                id,
                GameSource.Steam,
                "123",
                "Test Game",
                "PC",
                3600,
                now.AddDays(-1),
                now.AddMinutes(1)));

        var records = await repository.GetBySourceAsync(GameSource.Steam);
        Assert.Equal(1, records.Count);
        Assert.Equal<long?>(7200L, records[0].PlaytimeSeconds);
        Assert.Equal<DateTimeOffset?>(now.ToUniversalTime(), records[0].LastPlayedUtc);
    }

    internal static async Task HistoryServiceKeepsPlaytimeSeparate()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "launcher.db");
        var gameRepository = new SqliteGameRepository(path);
        var historyRepository = new SqliteGameHistoryRepository(path);
        var now = DateTimeOffset.UtcNow;

        var game = new Game(Guid.NewGuid(), "Control", now, now);
        var installation = new GameInstallation(
            Guid.NewGuid(),
            game.Id,
            GameSource.Steam,
            "870780",
            temp.Path,
            Path.Combine(temp.Path, "Control.exe"),
            "steam://rungameid/870780",
            true);

        await gameRepository.UpsertGameAsync(game);
        await gameRepository.UpsertInstallationAsync(installation);

        var session = new PlaySession(
            Guid.NewGuid(),
            game.Id,
            now.AddMinutes(-2),
            now,
            120);
        await gameRepository.AddPlaySessionAsync(session);

        await historyRepository.UpsertAsync(
            new GameHistoryRecord(
                StableId.FromText("history:playstation:control"),
                GameSource.PlayStation,
                "control-ps5",
                "Control",
                "PS5",
                3600,
                now.AddHours(-1),
                now));

        var library = new GameLibraryService(
            gameRepository,
            Array.Empty<IGameSourceAdapter>());

        var service = new GameHistoryService(
            historyRepository,
            new SqliteSteamHistorySettingsRepository(
                path,
                new PrefixSecretProtector()),
            new SteamHistoryApiClient(
                new HttpClient(
                    new StaticHttpHandler(
                        Encoding.UTF8.GetBytes("""{"response":{"game_count":0,"games":[]}}""")))),
            Array.Empty<GameLauncher.Core.History.IHistoryFileParser>(),
            library);

        var history = await service.GetHistoryAsync();

        Assert.Equal(1, history.Count);
        Assert.Equal("Control", history[0].Title);
        Assert.Equal(3600L, history[0].KnownAccountPlaytimeSeconds);
        Assert.Equal(120L, history[0].LauncherTrackedSeconds);
        Assert.True(history[0].IsInstalled);
        Assert.Equal(1, history[0].AccountRecords.Count);
    }
}
