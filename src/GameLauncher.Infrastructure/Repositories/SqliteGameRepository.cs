using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqliteGameRepository : IGameRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteGameRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("A database path is required.", nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS games (
                    id TEXT PRIMARY KEY NOT NULL,
                    title TEXT NOT NULL,
                    created_utc TEXT NOT NULL,
                    updated_utc TEXT NOT NULL,
                    cover_image_path TEXT NULL
                );

                CREATE TABLE IF NOT EXISTS game_installations (
                    id TEXT PRIMARY KEY NOT NULL,
                    game_id TEXT NOT NULL,
                    source INTEGER NOT NULL,
                    external_id TEXT NOT NULL,
                    install_path TEXT NULL,
                    executable_path TEXT NULL,
                    launch_uri TEXT NULL,
                    is_installed INTEGER NOT NULL,
                    launch_arguments TEXT NULL,
                    FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ux_game_installations_source_external
                    ON game_installations(source, external_id);

                CREATE INDEX IF NOT EXISTS ix_game_installations_game_id
                    ON game_installations(game_id);

                CREATE TABLE IF NOT EXISTS play_sessions (
                    id TEXT PRIMARY KEY NOT NULL,
                    game_id TEXT NOT NULL,
                    started_utc TEXT NOT NULL,
                    ended_utc TEXT NULL,
                    duration_seconds INTEGER NULL,
                    FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_play_sessions_game_id
                    ON play_sessions(game_id);

                CREATE TABLE IF NOT EXISTS process_rules (
                    id TEXT PRIMARY KEY NOT NULL,
                    game_id TEXT NOT NULL,
                    process_name TEXT NOT NULL,
                    executable_path TEXT NULL,
                    match_type INTEGER NOT NULL,
                    is_primary INTEGER NOT NULL,
                    FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_process_rules_game_id
                    ON process_rules(game_id);
                """;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await EnsureColumnAsync(connection, "games", "cover_image_path", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "game_installations", "launch_arguments", "TEXT NULL", cancellationToken);

        await using (var cleanup = connection.CreateCommand())
        {
            cleanup.CommandText = """
                DELETE FROM games
                WHERE id = '6d2ee65c-8cbe-4597-baed-5143405d02c1'
                  AND title = 'Phase 0 Test Game';
                """;
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
        }

        _initialized = true;
    }

    public async Task UpsertGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ValidateGame(game);
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO games (id, title, created_utc, updated_utc, cover_image_path)
            VALUES ($id, $title, $createdUtc, $updatedUtc, $coverImagePath)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                updated_utc = excluded.updated_utc,
                cover_image_path = COALESCE(excluded.cover_image_path, games.cover_image_path);
            """;

        command.Parameters.AddWithValue("$id", game.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", game.Title.Trim());
        command.Parameters.AddWithValue("$createdUtc", game.CreatedUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", game.UpdatedUtc.ToUniversalTime().ToString("O"));
        AddNullableText(command, "$coverImagePath", game.CoverImagePath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Game?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, created_utc, updated_utc, cover_image_path
            FROM games
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", gameId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return ReadGame(reader);
    }

    public async Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var games = new List<Game>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, created_utc, updated_utc, cover_image_path
            FROM games
            ORDER BY title COLLATE NOCASE;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) games.Add(ReadGame(reader));
        return games;
    }

    public async Task UpsertInstallationAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ValidateInstallation(installation);
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO game_installations (
                id, game_id, source, external_id, install_path,
                executable_path, launch_uri, is_installed, launch_arguments)
            VALUES (
                $id, $gameId, $source, $externalId, $installPath,
                $executablePath, $launchUri, $isInstalled, $launchArguments)
            ON CONFLICT(id) DO UPDATE SET
                game_id = excluded.game_id,
                source = excluded.source,
                external_id = excluded.external_id,
                install_path = excluded.install_path,
                executable_path = COALESCE(excluded.executable_path, game_installations.executable_path),
                launch_uri = excluded.launch_uri,
                is_installed = excluded.is_installed,
                launch_arguments = excluded.launch_arguments;
            """;

        command.Parameters.AddWithValue("$id", installation.Id.ToString("D"));
        command.Parameters.AddWithValue("$gameId", installation.GameId.ToString("D"));
        command.Parameters.AddWithValue("$source", (int)installation.Source);
        command.Parameters.AddWithValue("$externalId", installation.ExternalId.Trim());
        AddNullableText(command, "$installPath", installation.InstallPath);
        AddNullableText(command, "$executablePath", installation.ExecutablePath);
        AddNullableText(command, "$launchUri", installation.LaunchUri);
        command.Parameters.AddWithValue("$isInstalled", installation.IsInstalled ? 1 : 0);
        AddNullableText(command, "$launchArguments", installation.LaunchArguments);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GameInstallation>> GetInstallationsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var installations = new List<GameInstallation>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, game_id, source, external_id, install_path,
                   executable_path, launch_uri, is_installed, launch_arguments
            FROM game_installations
            WHERE game_id = $gameId
            ORDER BY source, external_id;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            installations.Add(new GameInstallation(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                (GameSource)reader.GetInt32(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetInt32(7) == 1,
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return installations;
    }

    public async Task AddPlaySessionAsync(PlaySession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Id == Guid.Empty) throw new ArgumentException("Session ID cannot be empty.", nameof(session));
        if (session.GameId == Guid.Empty) throw new ArgumentException("Game ID cannot be empty.", nameof(session));

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO play_sessions (id, game_id, started_utc, ended_utc, duration_seconds)
            VALUES ($id, $gameId, $startedUtc, $endedUtc, $durationSeconds);
            """;
        command.Parameters.AddWithValue("$id", session.Id.ToString("D"));
        command.Parameters.AddWithValue("$gameId", session.GameId.ToString("D"));
        command.Parameters.AddWithValue("$startedUtc", session.StartedUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$endedUtc", session.EndedUtc is null ? DBNull.Value : session.EndedUtc.Value.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$durationSeconds", session.DurationSeconds is null ? DBNull.Value : session.DurationSeconds.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task EndPlaySessionAsync(
        Guid sessionId,
        DateTimeOffset endedUtc,
        long durationSeconds,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("Session ID cannot be empty.", nameof(sessionId));
        if (durationSeconds < 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE play_sessions
            SET ended_utc = $endedUtc,
                duration_seconds = $durationSeconds
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", sessionId.ToString("D"));
        command.Parameters.AddWithValue("$endedUtc", endedUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$durationSeconds", durationSeconds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<long> GetTotalPlaytimeSecondsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(duration_seconds), 0)
            FROM play_sessions
            WHERE game_id = $gameId AND duration_seconds IS NOT NULL;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString("D"));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result ?? 0L);
    }

    public async Task<DateTimeOffset?> GetLastPlayedUtcAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MAX(ended_utc)
            FROM play_sessions
            WHERE game_id = $gameId AND ended_utc IS NOT NULL;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString("D"));
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is null or DBNull) return null;
        return DateTimeOffset.Parse(Convert.ToString(result)!);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string table,
        string column,
        string definition,
        CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await check.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        }

        await reader.DisposeAsync();
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Game ReadGame(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3)),
            reader.IsDBNull(4) ? null : reader.GetString(4));

    private static void AddNullableText(SqliteCommand command, string name, string? value) =>
        command.Parameters.AddWithValue(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim());

    private static void ValidateGame(Game game)
    {
        if (game.Id == Guid.Empty) throw new ArgumentException("Game ID cannot be empty.", nameof(game));
        if (string.IsNullOrWhiteSpace(game.Title)) throw new ArgumentException("Game title is required.", nameof(game));
    }

    private static void ValidateInstallation(GameInstallation installation)
    {
        if (installation.Id == Guid.Empty) throw new ArgumentException("Installation ID cannot be empty.", nameof(installation));
        if (installation.GameId == Guid.Empty) throw new ArgumentException("Game ID cannot be empty.", nameof(installation));
        if (string.IsNullOrWhiteSpace(installation.ExternalId))
        {
            throw new ArgumentException("External ID is required.", nameof(installation));
        }
    }
}
