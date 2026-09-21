using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Security;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqliteSteamHistorySettingsRepository : ISteamHistorySettingsRepository
{
    private readonly string _connectionString;
    private readonly ISecretProtector _protector;
    private bool _initialized;

    public SqliteSteamHistorySettingsRepository(
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

    public async Task<SteamHistorySettings?> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT steam_id64, protected_api_key, auto_sync
            FROM steam_history_settings
            WHERE id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        try
        {
            return new SteamHistorySettings(
                reader.GetString(0),
                _protector.Unprotect(reader.GetString(1)),
                reader.GetInt32(2) == 1).Normalize();
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(
        SteamHistorySettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();
        if (!settings.IsConfigured)
        {
            throw new ArgumentException("Steam history settings are incomplete.", nameof(settings));
        }

        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO steam_history_settings(id, steam_id64, protected_api_key, auto_sync)
            VALUES(1, $steamId, $apiKey, $autoSync)
            ON CONFLICT(id) DO UPDATE SET
                steam_id64 = excluded.steam_id64,
                protected_api_key = excluded.protected_api_key,
                auto_sync = excluded.auto_sync;
            """;
        command.Parameters.AddWithValue("$steamId", settings.SteamId64);
        command.Parameters.AddWithValue("$apiKey", _protector.Protect(settings.ApiKey));
        command.Parameters.AddWithValue("$autoSync", settings.AutoSync ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM steam_history_settings WHERE id = 1;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS steam_history_settings(
                id INTEGER PRIMARY KEY CHECK(id=1),
                steam_id64 TEXT NOT NULL,
                protected_api_key TEXT NOT NULL,
                auto_sync INTEGER NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        _initialized = true;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
