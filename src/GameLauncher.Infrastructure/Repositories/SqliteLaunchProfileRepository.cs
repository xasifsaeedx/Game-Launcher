using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqliteLaunchProfileRepository : ILaunchProfileRepository
{
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteLaunchProfileRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<IReadOnlyList<LaunchProfile>> GetLaunchProfilesAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var profiles = new List<LaunchProfile>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, game_id, name, installation_id, game_arguments_override,
                   is_default, created_utc, updated_utc
            FROM launch_profiles
            WHERE game_id = $gameId
            ORDER BY is_default DESC, name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(ReadProfile(reader));
        }

        return profiles;
    }

    public async Task<LaunchProfile?> GetLaunchProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, game_id, name, installation_id, game_arguments_override,
                   is_default, created_utc, updated_utc
            FROM launch_profiles
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", profileId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProfile(reader) : null;
    }

    public async Task<IReadOnlyList<LaunchAction>> GetLaunchActionsAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        var actions = new List<LaunchAction>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, profile_id, stage, name, executable_path, arguments,
                   working_directory, sort_order, wait_for_exit, close_with_game, is_enabled
            FROM launch_actions
            WHERE profile_id = $profileId
            ORDER BY stage, sort_order, name COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$profileId", profileId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            actions.Add(new LaunchAction(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                (LaunchActionStage)reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8) == 1,
                reader.GetInt32(9) == 1,
                reader.GetInt32(10) == 1));
        }

        return actions;
    }

    public async Task UpsertLaunchProfileAsync(
        LaunchProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO launch_profiles (
                id, game_id, name, installation_id, game_arguments_override,
                is_default, created_utc, updated_utc)
            VALUES (
                $id, $gameId, $name, $installationId, $arguments,
                $isDefault, $createdUtc, $updatedUtc)
            ON CONFLICT(id) DO UPDATE SET
                game_id = excluded.game_id,
                name = excluded.name,
                installation_id = excluded.installation_id,
                game_arguments_override = excluded.game_arguments_override,
                is_default = excluded.is_default,
                updated_utc = excluded.updated_utc;
            """;

        command.Parameters.AddWithValue("$id", profile.Id.ToString("D"));
        command.Parameters.AddWithValue("$gameId", profile.GameId.ToString("D"));
        command.Parameters.AddWithValue("$name", profile.Name.Trim());
        command.Parameters.AddWithValue(
            "$installationId",
            profile.InstallationId.HasValue ? profile.InstallationId.Value.ToString("D") : DBNull.Value);
        command.Parameters.AddWithValue(
            "$arguments",
            string.IsNullOrWhiteSpace(profile.GameArgumentsOverride)
                ? DBNull.Value
                : profile.GameArgumentsOverride.Trim());
        command.Parameters.AddWithValue("$isDefault", profile.IsDefault ? 1 : 0);
        command.Parameters.AddWithValue("$createdUtc", profile.CreatedUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", profile.UpdatedUtc.ToUniversalTime().ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ReplaceLaunchActionsAsync(
        Guid profileId,
        IReadOnlyList<LaunchAction> actions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM launch_actions WHERE profile_id = $profileId;";
            delete.Parameters.AddWithValue("$profileId", profileId.ToString("D"));
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var action in actions)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO launch_actions (
                    id, profile_id, stage, name, executable_path, arguments,
                    working_directory, sort_order, wait_for_exit, close_with_game, is_enabled)
                VALUES (
                    $id, $profileId, $stage, $name, $executablePath, $arguments,
                    $workingDirectory, $sortOrder, $waitForExit, $closeWithGame, $isEnabled);
                """;

            insert.Parameters.AddWithValue("$id", action.Id.ToString("D"));
            insert.Parameters.AddWithValue("$profileId", profileId.ToString("D"));
            insert.Parameters.AddWithValue("$stage", (int)action.Stage);
            insert.Parameters.AddWithValue("$name", action.Name.Trim());
            insert.Parameters.AddWithValue("$executablePath", action.ExecutablePath.Trim());
            insert.Parameters.AddWithValue("$arguments", DbText(action.Arguments));
            insert.Parameters.AddWithValue("$workingDirectory", DbText(action.WorkingDirectory));
            insert.Parameters.AddWithValue("$sortOrder", action.SortOrder);
            insert.Parameters.AddWithValue("$waitForExit", action.WaitForExit ? 1 : 0);
            insert.Parameters.AddWithValue("$closeWithGame", action.CloseWithGame ? 1 : 0);
            insert.Parameters.AddWithValue("$isEnabled", action.IsEnabled ? 1 : 0);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteLaunchProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM launch_profiles WHERE id = $id;";
        command.Parameters.AddWithValue("$id", profileId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearDefaultLaunchProfilesAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE launch_profiles
            SET is_default = 0
            WHERE game_id = $gameId;
            """;
        command.Parameters.AddWithValue("$gameId", gameId.ToString("D"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS launch_profiles (
                id TEXT PRIMARY KEY NOT NULL,
                game_id TEXT NOT NULL,
                name TEXT NOT NULL,
                installation_id TEXT NULL,
                game_arguments_override TEXT NULL,
                is_default INTEGER NOT NULL DEFAULT 0,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL,
                FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_launch_profiles_game_id
                ON launch_profiles(game_id);

            CREATE TABLE IF NOT EXISTS launch_actions (
                id TEXT PRIMARY KEY NOT NULL,
                profile_id TEXT NOT NULL,
                stage INTEGER NOT NULL,
                name TEXT NOT NULL,
                executable_path TEXT NOT NULL,
                arguments TEXT NULL,
                working_directory TEXT NULL,
                sort_order INTEGER NOT NULL,
                wait_for_exit INTEGER NOT NULL,
                close_with_game INTEGER NOT NULL,
                is_enabled INTEGER NOT NULL,
                FOREIGN KEY (profile_id) REFERENCES launch_profiles(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_launch_actions_profile_id
                ON launch_actions(profile_id);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        _initialized = true;
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

    private static LaunchProfile ReadProfile(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : Guid.Parse(reader.GetString(3)),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetInt32(5) == 1,
            DateTimeOffset.Parse(reader.GetString(6)),
            DateTimeOffset.Parse(reader.GetString(7)));

    private static object DbText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}
