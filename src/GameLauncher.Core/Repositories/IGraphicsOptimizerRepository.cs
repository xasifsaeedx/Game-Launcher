using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface IGraphicsOptimizerRepository
{
    Task<GraphicsOptimizerSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(
        GraphicsOptimizerSettings settings,
        CancellationToken cancellationToken = default);
}
