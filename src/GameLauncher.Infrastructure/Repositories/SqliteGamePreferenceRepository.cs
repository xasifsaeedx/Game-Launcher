using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqliteGamePreferenceRepository : IGamePreferenceRepository
{
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteGamePreferenceRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<GamePreference?> GetAsync(
        string gameKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameKey);
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT game_key, is_favorite, updated_utc
            FROM game_preferences
            WHERE game_key = $gameKey;
            """;
        command.Parameters.AddWithValue("$gameKey", gameKey.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new GamePreference(
            reader.GetString(0),
            reader.GetInt32(1) == 1,
            DateTimeOffset.Parse(reader.GetString(2)));
    }

    public async Task<IReadOnlyList<GamePreference>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var results = new List<GamePreference>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT game_key, is_favorite, updated_utc
            FROM game_preferences
            ORDER BY game_key;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new GamePreference(
                reader.GetString(0),
                reader.GetInt32(1) == 1,
                DateTimeOffset.Parse(reader.GetString(2))));
        }

        return results;
    }

    public async Task SetFavoriteAsync(
        string gameKey,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameKey);
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO game_preferences(game_key, is_favorite, updated_utc)
            VALUES($gameKey, $favorite, $updated)
            ON CONFLICT(game_key) DO UPDATE SET
                is_favorite = excluded.is_favorite,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$gameKey", gameKey.Trim());
        command.Parameters.AddWithValue("$favorite", isFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS game_preferences(
                game_key TEXT PRIMARY KEY NOT NULL,
                is_favorite INTEGER NOT NULL,
                updated_utc TEXT NOT NULL
            );
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
}
