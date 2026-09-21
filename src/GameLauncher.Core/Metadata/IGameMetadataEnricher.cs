using GameLauncher.Core.Models;

namespace GameLauncher.Core.Metadata;

public interface IGameMetadataEnricher
{
    Task<Game> EnrichAsync(
        Game game,
        IReadOnlyList<GameInstallation> installations,
        CancellationToken cancellationToken = default);
}
