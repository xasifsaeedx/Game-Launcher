using GameLauncher.Core.Models;

namespace GameLauncher.Core.Runtime;

public interface IGameplayOverlayRuntime
{
    Task<IOverlaySession> StartAsync(
        int processId,
        OverlaySettings settings,
        CancellationToken cancellationToken = default);

    Task<OverlayRuntimeStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);
}
