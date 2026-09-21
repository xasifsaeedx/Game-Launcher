using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Security;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqliteHatchableSyncRepository : IHatchableSyncRepository
{
    private readonly string _connectionString;
    private readonly ISecretProtector _protector;
    private bool _initialized;

    public SqliteHatchableSyncRepository(
        string databasePath,
        ISecretProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<HatchableSyncSettings?> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT base_url, protected_token, auto_sync
            FROM hatchable_sync_settings
            WHERE id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        try
        {
            return new HatchableSyncSettings(
                reader.GetString(0),
                _protector.Unprotect(reader.GetString(1)),
                reader.GetInt32(2) == 1).Normalize();
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveSettingsAsync(
        HatchableSyncSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();

        await InitializeAsync(cancellationToken);
        var protectedToken = _protector.Protect(settings.Token);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO hatchable_sync_settings(id, base_url, protected_token, auto_sync)
            VALUES(1, $baseUrl, $protectedToken, $autoSync)
            ON CONFLICT(id) DO UPDATE SET
                base_url = excluded.base_url,
                protected_token = excluded.protected_token,
                auto_sync = excluded.auto_sync;
            """;
        command.Parameters.AddWithValue("$baseUrl", settings.BaseUrl);
        command.Parameters.AddWithValue("$protectedToken", protectedToken);
        command.Parameters.AddWithValue("$autoSync", settings.AutoSync ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM hatchable_sync_settings WHERE id=1;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ReplaceRemoteGamesAsync(
        IReadOnlyList<HatchableRemoteGame> games,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(games);
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM hatchable_remote_games;";
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var game in games)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO hatchable_remote_games(
                    remote_game_id, title, platforms, steam_app_id, cover_url,
                    rank_score, library_status, progress_status, rating,
                    playtime_seconds, last_played_at)
                VALUES(
                    $id, $title, $platforms, $steamAppId, $coverUrl,
                    $rank, $libraryStatus, $progressStatus, $rating,
                    $playtimeSeconds, $lastPlayedAt);
                """;
            insert.Parameters.AddWithValue("$id", game.RemoteGameId);
            insert.Parameters.AddWithValue("$title", game.Title);
            insert.Parameters.AddWithValue("$platforms", game.Platforms);
            AddNullable(insert, "$steamAppId", game.SteamAppId);
            AddNullable(insert, "$coverUrl", game.CoverUrl);
            insert.Parameters.AddWithValue("$rank", game.RankScore);
            AddNullable(insert, "$libraryStatus", game.LibraryStatus);
            AddNullable(insert, "$progressStatus", game.ProgressStatus);
            AddNullable(insert, "$rating", game.Rating);
            insert.Parameters.AddWithValue("$playtimeSeconds", game.PlaytimeSeconds);
            AddNullable(insert, "$lastPlayedAt", game.LastPlayedAt?.ToUniversalTime().ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
    }

    public async Task<IReadOnlyList<HatchableRemoteGame>> GetRemoteGamesAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var games = new List<HatchableRemoteGame>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT remote_game_id, title, platforms, steam_app_id, cover_url,
                   rank_score, library_status, progress_status, rating,
                   playtime_seconds, last_played_at
            FROM hatchable_remote_games
            ORDER BY rank_score, remote_game_id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(new HatchableRemoteGame(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetInt32(8),
                reader.GetInt64(9),
                reader.IsDBNull(10) ? null : DateTimeOffset.Parse(reader.GetString(10))));
        }

        return games;
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS hatchable_sync_settings(
                id INTEGER PRIMARY KEY CHECK(id=1),
                base_url TEXT NOT NULL,
                protected_token TEXT NOT NULL,
                auto_sync INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS hatchable_remote_games(
                remote_game_id INTEGER PRIMARY KEY,
                title TEXT NOT NULL,
                platforms TEXT NOT NULL,
                steam_app_id INTEGER NULL,
                cover_url TEXT NULL,
                rank_score INTEGER NOT NULL,
                library_status TEXT NULL,
                progress_status TEXT NULL,
                rating INTEGER NULL,
                playtime_seconds INTEGER NOT NULL,
                last_played_at TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_hatchable_remote_rank
                ON hatchable_remote_games(rank_score);
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
