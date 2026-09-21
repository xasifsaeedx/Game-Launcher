using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqliteOverlaySettingsRepository : IOverlaySettingsRepository
{
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteOverlaySettingsRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<OverlaySettings> GetAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT enabled, show_fps, show_gpu_usage, show_gpu_temperature, update_interval_ms
            FROM overlay_settings
            WHERE id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return OverlaySettings.Default;
        }

        return new OverlaySettings(
            reader.GetInt32(0) == 1,
            reader.GetInt32(1) == 1,
            reader.GetInt32(2) == 1,
            reader.GetInt32(3) == 1,
            reader.GetInt32(4)).Normalize();
    }

    public async Task SaveAsync(
        OverlaySettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();

        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO overlay_settings (
                id, enabled, show_fps, show_gpu_usage, show_gpu_temperature, update_interval_ms)
            VALUES (
                1, $enabled, $showFps, $showGpuUsage, $showGpuTemperature, $updateIntervalMs)
            ON CONFLICT(id) DO UPDATE SET
                enabled = excluded.enabled,
                show_fps = excluded.show_fps,
                show_gpu_usage = excluded.show_gpu_usage,
                show_gpu_temperature = excluded.show_gpu_temperature,
                update_interval_ms = excluded.update_interval_ms;
            """;

        command.Parameters.AddWithValue("$enabled", settings.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("$showFps", settings.ShowFps ? 1 : 0);
        command.Parameters.AddWithValue("$showGpuUsage", settings.ShowGpuUsage ? 1 : 0);
        command.Parameters.AddWithValue("$showGpuTemperature", settings.ShowGpuTemperature ? 1 : 0);
        command.Parameters.AddWithValue("$updateIntervalMs", settings.UpdateIntervalMs);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS overlay_settings (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                enabled INTEGER NOT NULL,
                show_fps INTEGER NOT NULL,
                show_gpu_usage INTEGER NOT NULL,
                show_gpu_temperature INTEGER NOT NULL,
                update_interval_ms INTEGER NOT NULL
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
