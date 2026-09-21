using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface ISteamHistorySettingsRepository
{
    Task<SteamHistorySettings?> GetAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        SteamHistorySettings settings,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        CancellationToken cancellationToken = default);
}
