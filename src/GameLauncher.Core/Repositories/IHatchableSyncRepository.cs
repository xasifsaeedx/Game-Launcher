using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface IHatchableSyncRepository
{
    Task<HatchableSyncSettings?> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(
        HatchableSyncSettings settings,
        CancellationToken cancellationToken = default);

    Task ClearSettingsAsync(
        CancellationToken cancellationToken = default);

    Task ReplaceRemoteGamesAsync(
        IReadOnlyList<HatchableRemoteGame> games,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HatchableRemoteGame>> GetRemoteGamesAsync(
        CancellationToken cancellationToken = default);
}
