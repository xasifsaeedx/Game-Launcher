using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Repositories;

public sealed class SqliteGraphicsOptimizerRepository : IGraphicsOptimizerRepository
{
    private readonly string _connectionString;
    private bool _initialized;

    public SqliteGraphicsOptimizerRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<GraphicsOptimizerSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT target_width, target_height, target_fps, quality_preference,
                   tier_override, auto_apply_before_launch
            FROM graphics_optimizer_settings
            WHERE id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return GraphicsOptimizerSettings.Default;
        }

        return new GraphicsOptimizerSettings(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            (GraphicsQualityPreference)reader.GetInt32(3),
            reader.IsDBNull(4) ? null : (GraphicsPerformanceTier)reader.GetInt32(4),
            reader.GetInt32(5) == 1).Normalize();
    }

    public async Task SaveSettingsAsync(
        GraphicsOptimizerSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO graphics_optimizer_settings(
                id, target_width, target_height, target_fps, quality_preference,
                tier_override, auto_apply_before_launch)
            VALUES(1, $width, $height, $fps, $preference, $tier, $autoApply)
            ON CONFLICT(id) DO UPDATE SET
                target_width = excluded.target_width,
                target_height = excluded.target_height,
                target_fps = excluded.target_fps,
                quality_preference = excluded.quality_preference,
                tier_override = excluded.tier_override,
                auto_apply_before_launch = excluded.auto_apply_before_launch;
            """;
        command.Parameters.AddWithValue("$width", settings.TargetWidth);
        command.Parameters.AddWithValue("$height", settings.TargetHeight);
        command.Parameters.AddWithValue("$fps", settings.TargetFps);
        command.Parameters.AddWithValue("$preference", (int)settings.QualityPreference);
        command.Parameters.AddWithValue(
            "$tier",
            settings.TierOverride.HasValue
                ? (object)(int)settings.TierOverride.Value
                : DBNull.Value);
        command.Parameters.AddWithValue("$autoApply", settings.AutoApplyBeforeLaunch ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS graphics_optimizer_settings(
                id INTEGER PRIMARY KEY CHECK(id=1),
                target_width INTEGER NOT NULL,
                target_height INTEGER NOT NULL,
                target_fps INTEGER NOT NULL,
                quality_preference INTEGER NOT NULL,
                tier_override INTEGER NULL,
                auto_apply_before_launch INTEGER NOT NULL
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
