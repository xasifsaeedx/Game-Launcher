using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqlitePersonalLibraryRepository : IPersonalLibraryRepository
{
    private readonly string _connectionString;
    private bool _initialized;

    public SqlitePersonalLibraryRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<PersonalLibrarySettings?> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sheet_url, auto_sync
            FROM personal_library_settings
            WHERE id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new PersonalLibrarySettings(
            reader.GetString(0),
            reader.GetInt32(1) == 1).Normalize();
    }

    public async Task SaveSettingsAsync(
        PersonalLibrarySettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO personal_library_settings(id, sheet_url, auto_sync)
            VALUES(1, $sheetUrl, $autoSync)
            ON CONFLICT(id) DO UPDATE SET
                sheet_url = excluded.sheet_url,
                auto_sync = excluded.auto_sync;
            """;
        command.Parameters.AddWithValue("$sheetUrl", settings.SheetUrl);
        command.Parameters.AddWithValue("$autoSync", settings.AutoSync ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM personal_library_settings WHERE id = 1;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ReplaceGamesAsync(
        IReadOnlyList<PersonalLibraryGame> games,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(games);
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM personal_library_games;";
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var game in games)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO personal_library_games(
                    source_row, title, platforms, steam_app_id, cover_url,
                    rank_value, library_status, progress_status, sheet_rating)
                VALUES(
                    $row, $title, $platforms, $steamAppId, $coverUrl,
                    $rank, $libraryStatus, $progressStatus, $sheetRating);
                """;

            insert.Parameters.AddWithValue("$row", game.SourceRow);
            insert.Parameters.AddWithValue("$title", game.Title);
            insert.Parameters.AddWithValue("$platforms", game.Platforms);
            AddNullable(insert, "$steamAppId", game.SteamAppId);
            AddNullable(insert, "$coverUrl", game.CoverUrl);
            AddNullable(insert, "$rank", game.Rank);
            AddNullable(insert, "$libraryStatus", game.LibraryStatus);
            AddNullable(insert, "$progressStatus", game.ProgressStatus);
            AddNullable(insert, "$sheetRating", game.SheetRating);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<PersonalLibraryGame>> GetGamesAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var games = new List<PersonalLibraryGame>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_row, title, platforms, steam_app_id, cover_url,
                   rank_value, library_status, progress_status, sheet_rating
            FROM personal_library_games
            ORDER BY CASE WHEN rank_value IS NULL THEN 1 ELSE 0 END,
                     rank_value,
                     source_row;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(new PersonalLibraryGame(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8)));
        }

        return games;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS personal_library_settings(
                id INTEGER PRIMARY KEY CHECK(id = 1),
                sheet_url TEXT NOT NULL,
                auto_sync INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS personal_library_games(
                source_row INTEGER PRIMARY KEY,
                title TEXT NOT NULL,
                platforms TEXT NOT NULL,
                steam_app_id INTEGER NULL,
                cover_url TEXT NULL,
                rank_value INTEGER NULL,
                library_status TEXT NULL,
                progress_status TEXT NULL,
                sheet_rating INTEGER NULL
            );

            CREATE INDEX IF NOT EXISTS ix_personal_library_rank
                ON personal_library_games(rank_value);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _initialized = true;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddNullable(
        SqliteCommand command,
        string name,
        object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
}
