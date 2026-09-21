using GameLauncher.Core.Models;

namespace GameLauncher.Core.Graphics;

public interface IGraphicsConfigRuntime
{
    Task<GraphicsApplyResult> ApplyAsync(
        GraphicsRecommendation recommendation,
        HardwareProfile hardware,
        CancellationToken cancellationToken = default);

    Task<GraphicsApplyResult> RestoreAsync(
        GraphicsRecommendation recommendation,
        CancellationToken cancellationToken = default);
}
