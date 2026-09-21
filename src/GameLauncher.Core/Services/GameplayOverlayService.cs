using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Runtime;

namespace GameLauncher.Core.Services;

public sealed class GameplayOverlayService
{
    private readonly IOverlaySettingsRepository _settings;
    private readonly IGameplayOverlayRuntime _runtime;

    public GameplayOverlayService(
        IOverlaySettingsRepository settings,
        IGameplayOverlayRuntime runtime)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public Task<OverlaySettings> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        _settings.GetAsync(cancellationToken);

    public Task<OverlayRuntimeStatus> GetStatusAsync(
        CancellationToken cancellationToken = default) =>
        _runtime.GetStatusAsync(cancellationToken);

    public Task SaveSettingsAsync(
        OverlaySettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _settings.SaveAsync(settings.Normalize(), cancellationToken);
    }

    public async Task<IOverlaySession> StartForGameAsync(
        int? processId,
        CancellationToken cancellationToken = default)
    {
        if (!processId.HasValue || processId.Value <= 0)
        {
            return NoopOverlaySession.Instance;
        }

        var settings = (await _settings.GetAsync(cancellationToken)).Normalize();
        if (!settings.Enabled ||
            (!settings.ShowFps &&
             !settings.ShowGpuUsage &&
             !settings.ShowGpuTemperature))
        {
            return NoopOverlaySession.Instance;
        }

        try
        {
            return await _runtime.StartAsync(
                processId.Value,
                settings,
                cancellationToken);
        }
        catch
        {
            // Overlay failure must never prevent a game from launching/tracking.
            return NoopOverlaySession.Instance;
        }
    }

    private sealed class NoopOverlaySession : IOverlaySession
    {
        public static NoopOverlaySession Instance { get; } = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
