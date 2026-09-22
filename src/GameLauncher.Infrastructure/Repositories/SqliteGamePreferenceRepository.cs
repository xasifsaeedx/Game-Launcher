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
            SELECT game_key, is_favorite, rating, updated_utc
            FROM game_preferences
            WHERE game_key = $gameKey;
            """;
        command.Parameters.AddWithValue("$gameKey", gameKey.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadPreference(reader);
    }

    public async Task<IReadOnlyList<GamePreference>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var results = new List<GamePreference>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT game_key, is_favorite, rating, updated_utc
            FROM game_preferences
            ORDER BY game_key;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadPreference(reader));
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
            INSERT INTO game_preferences(game_key, is_favorite, rating, updated_utc)
            VALUES($gameKey, $favorite, NULL, $updated)
            ON CONFLICT(game_key) DO UPDATE SET
                is_favorite = excluded.is_favorite,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$gameKey", gameKey.Trim());
        command.Parameters.AddWithValue("$favorite", isFavorite ? 1 : 0);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SetRatingAsync(
        string gameKey,
        int? rating,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameKey);
        if (rating is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be 1-10.");
        }

        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO game_preferences(game_key, is_favorite, rating, updated_utc)
            VALUES($gameKey, 0, $rating, $updated)
            ON CONFLICT(game_key) DO UPDATE SET
                rating = excluded.rating,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$gameKey", gameKey.Trim());
        command.Parameters.AddWithValue("$rating", rating ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS game_preferences(
                    game_key TEXT PRIMARY KEY NOT NULL,
                    is_favorite INTEGER NOT NULL,
                    rating INTEGER NULL,
                    updated_utc TEXT NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await EnsureRatingColumnAsync(connection, cancellationToken);
        _initialized = true;
    }

    private static async Task EnsureRatingColumnAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var info = connection.CreateCommand();
        info.CommandText = "PRAGMA table_info(game_preferences);";

        var hasRating = false;
        await using (var reader = await info.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), "rating", StringComparison.OrdinalIgnoreCase))
                {
                    hasRating = true;
                    break;
                }
            }
        }

        if (hasRating) return;

        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE game_preferences ADD COLUMN rating INTEGER NULL;";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static GamePreference ReadPreference(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            reader.GetInt32(1) == 1,
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            DateTimeOffset.Parse(reader.GetString(3)));

    private async Task<SqliteConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
