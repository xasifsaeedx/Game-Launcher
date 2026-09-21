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
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS games (
                id TEXT PRIMARY KEY NOT NULL,
                title TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
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
            INSERT INTO games (id, title, created_utc, updated_utc)
            VALUES ($id, $title, $createdUtc, $updatedUtc)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                updated_utc = excluded.updated_utc;
            """;

        command.Parameters.AddWithValue("$id", game.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", game.Title.Trim());
        command.Parameters.AddWithValue("$createdUtc", game.CreatedUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", game.UpdatedUtc.ToUniversalTime().ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
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
                executable_path, launch_uri, is_installed)
            VALUES (
                $id, $gameId, $source, $externalId, $installPath,
                $executablePath, $launchUri, $isInstalled)
            ON CONFLICT(id) DO UPDATE SET
                game_id = excluded.game_id,
                source = excluded.source,
                external_id = excluded.external_id,
                install_path = excluded.install_path,
                executable_path = excluded.executable_path,
                launch_uri = excluded.launch_uri,
                is_installed = excluded.is_installed;
            """;

        command.Parameters.AddWithValue("$id", installation.Id.ToString("D"));
        command.Parameters.AddWithValue("$gameId", installation.GameId.ToString("D"));
        command.Parameters.AddWithValue("$source", (int)installation.Source);
        command.Parameters.AddWithValue("$externalId", installation.ExternalId.Trim());
        AddNullableText(command, "$installPath", installation.InstallPath);
        AddNullableText(command, "$executablePath", installation.ExecutablePath);
        AddNullableText(command, "$launchUri", installation.LaunchUri);
        command.Parameters.AddWithValue("$isInstalled", installation.IsInstalled ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var games = new List<Game>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, created_utc, updated_utc
            FROM games
            ORDER BY title COLLATE NOCASE;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            games.Add(new Game(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2)),
                DateTimeOffset.Parse(reader.GetString(3))));
        }

        return games;
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
                   executable_path, launch_uri, is_installed
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
                reader.GetInt32(7) == 1));
        }

        return installations;
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

    private static void AddNullableText(SqliteCommand command, string name, string? value)
    {
        command.Parameters.AddWithValue(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim());
    }

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
