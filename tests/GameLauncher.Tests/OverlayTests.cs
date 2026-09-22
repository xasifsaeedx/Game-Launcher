namespace GameLauncher.Tests;

internal static class OverlayTests
{
    internal static Task OverlayFormatterEmitsRequestedTelemetry()
    {
        var settings = new OverlaySettings(true, true, true, true, 500);
        var text = OverlayTextFormatter.Format(
            settings,
            new OverlayMetrics(59.6, 73.2, 64.8));

        Assert.Equal("FPS 60  |  GPU 73%  |  TEMP 65C", text);

        var gpuOnly = OverlayTextFormatter.Format(
            settings with { ShowFps = false, ShowGpuTemperature = false },
            new OverlayMetrics(120, 51.4, 70));

        Assert.Equal("GPU 51%", gpuOnly);
        return Task.CompletedTask;
    }

    internal static async Task OverlaySettingsPersistAndNormalize()
    {
        using var temp = new TempDirectory();
        var repository = new SqliteOverlaySettingsRepository(
            Path.Combine(temp.Path, "launcher.db"));

        await repository.SaveAsync(
            new OverlaySettings(true, true, false, true, 50));

        var stored = await repository.GetAsync();

        Assert.True(stored.Enabled);
        Assert.True(stored.ShowFps);
        Assert.True(!stored.ShowGpuUsage);
        Assert.True(stored.ShowGpuTemperature);
        Assert.Equal(250, stored.UpdateIntervalMs);
    }

    internal static async Task OverlayServiceSkipsDisabledOverlay()
    {
        var settings = new MemoryOverlaySettingsRepository(
            new OverlaySettings(false, true, true, true, 500));
        var runtime = new RecordingOverlayRuntime(new List<string>());
        var service = new GameplayOverlayService(settings, runtime);

        await using var session = await service.StartForGameAsync(1234);

        Assert.Equal(0, runtime.StartCount);
    }

    internal static async Task SessionServiceOwnsOverlayLifecycle()
    {
        using var temp = new TempDirectory();
        var repository = new SqliteGameRepository(Path.Combine(temp.Path, "launcher.db"));
        var events = new List<string>();
        var now = DateTimeOffset.UtcNow;

        var game = new Game(Guid.NewGuid(), "Overlay Lifecycle", now, now);
        var installation = new GameInstallation(
            Guid.NewGuid(),
            game.Id,
            GameSource.Manual,
            "overlay-lifecycle",
            temp.Path,
            Path.Combine(temp.Path, "Game.exe"),
            null,
            true);

        await repository.UpsertGameAsync(game);
        await repository.UpsertInstallationAsync(installation);

        var settings = new MemoryOverlaySettingsRepository(OverlaySettings.Default);
        var overlayRuntime = new RecordingOverlayRuntime(events);
        var overlay = new GameplayOverlayService(settings, overlayRuntime);
        var runtime = new OverlayAwareGameRuntime(events, now, now.AddSeconds(10));
        var sessions = new GameSessionService(repository, runtime, overlay);

        await sessions.LaunchAndTrackAsync(installation);

        Assert.SequenceEqual(
            new[]
            {
                "game:start",
                "overlay:start:4242",
                "game:wait",
                "overlay:dispose"
            },
            events);
    }
}
