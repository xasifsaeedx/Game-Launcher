using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface IOverlaySettingsRepository
{
    Task<OverlaySettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        OverlaySettings settings,
        CancellationToken cancellationToken = default);
}
