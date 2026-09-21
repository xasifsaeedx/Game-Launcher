using GameLauncher.Core.Models;

namespace GameLauncher.Core.Sync;

public interface IHatchableApiClient
{
    Task<IReadOnlyList<HatchableRemoteGame>> GetGamesAsync(
        HatchableSyncSettings settings,
        CancellationToken cancellationToken = default);

    Task<int> PushGamesAsync(
        HatchableSyncSettings settings,
        IReadOnlyList<HatchableGamePush> games,
        CancellationToken cancellationToken = default);
}
