using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqliteGameHistoryRepository : IGameHistoryRepository
{
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteGameHistoryRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS game_history (
                id TEXT PRIMARY KEY NOT NULL,
                source INTEGER NOT NULL,
                external_id TEXT NOT NULL,
                title TEXT NOT NULL,
                platform TEXT NULL,
                playtime_seconds INTEGER NULL,
                last_played_utc TEXT NULL,
                imported_utc TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_game_history_source_external
                ON game_history(source, external_id);

            CREATE INDEX IF NOT EXISTS ix_game_history_title
                ON game_history(title COLLATE NOCASE);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _initialized = true;
    }

    public async Task UpsertAsync(
        GameHistoryRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Id == Guid.Empty) throw new ArgumentException("History ID cannot be empty.", nameof(record));
        if (record.Source == GameSource.Unknown) throw new ArgumentException("History source is required.", nameof(record));
        if (string.IsNullOrWhiteSpace(record.ExternalId)) throw new ArgumentException("External ID is required.", nameof(record));
        if (string.IsNullOrWhiteSpace(record.Title)) throw new ArgumentException("Title is required.", nameof(record));

        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO game_history(
                id, source, external_id, title, platform,
                playtime_seconds, last_played_utc, imported_utc)
            VALUES(
                $id, $source, $externalId, $title, $platform,
                $playtime, $lastPlayed, $imported)
            ON CONFLICT(source, external_id) DO UPDATE SET
                title = excluded.title,
                platform = COALESCE(excluded.platform, game_history.platform),
                playtime_seconds = CASE
                    WHEN excluded.playtime_seconds IS NULL THEN game_history.playtime_seconds
                    WHEN game_history.playtime_seconds IS NULL THEN excluded.playtime_seconds
                    ELSE MAX(game_history.playtime_seconds, excluded.playtime_seconds)
                END,
                last_played_utc = CASE
                    WHEN excluded.last_played_utc IS NULL THEN game_history.last_played_utc
                    WHEN game_history.last_played_utc IS NULL THEN excluded.last_played_utc
                    WHEN excluded.last_played_utc > game_history.last_played_utc THEN excluded.last_played_utc
                    ELSE game_history.last_played_utc
                END,
                imported_utc = excluded.imported_utc;
            """;

        command.Parameters.AddWithValue("$id", record.Id.ToString("D"));
        command.Parameters.AddWithValue("$source", (int)record.Source);
        command.Parameters.AddWithValue("$externalId", record.ExternalId.Trim());
        command.Parameters.AddWithValue("$title", record.Title.Trim());
        command.Parameters.AddWithValue("$platform", string.IsNullOrWhiteSpace(record.Platform) ? DBNull.Value : record.Platform.Trim());
        command.Parameters.AddWithValue("$playtime", record.PlaytimeSeconds.HasValue ? record.PlaytimeSeconds.Value : DBNull.Value);
        command.Parameters.AddWithValue("$lastPlayed", record.LastPlayedUtc.HasValue ? record.LastPlayedUtc.Value.ToUniversalTime().ToString("O") : DBNull.Value);
        command.Parameters.AddWithValue("$imported", record.ImportedUtc.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameHistoryRecord>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        return await ReadAsync(null, cancellationToken);
    }

    public async Task<IReadOnlyList<GameHistoryRecord>> GetBySourceAsync(
        GameSource source,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        return await ReadAsync(source, cancellationToken);
    }

    private async Task<IReadOnlyList<GameHistoryRecord>> ReadAsync(
        GameSource? source,
        CancellationToken cancellationToken)
    {
        var records = new List<GameHistoryRecord>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = source.HasValue
            ? """
              SELECT id, source, external_id, title, platform,
                     playtime_seconds, last_played_utc, imported_utc
              FROM game_history
              WHERE source = $source
              ORDER BY title COLLATE NOCASE;
              """
            : """
              SELECT id, source, external_id, title, platform,
                     playtime_seconds, last_played_utc, imported_utc
              FROM game_history
              ORDER BY title COLLATE NOCASE, source;
              """;

        if (source.HasValue)
        {
            command.Parameters.AddWithValue("$source", (int)source.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new GameHistoryRecord(
                Guid.Parse(reader.GetString(0)),
                (GameSource)reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt64(5),
                reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
                DateTimeOffset.Parse(reader.GetString(7))));
        }

        return records;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
