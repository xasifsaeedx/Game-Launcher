using GameLauncher.Core.Models;

namespace GameLauncher.Core.Adapters;

public interface IGameSourceAdapter
{
    string Id { get; }
    string DisplayName { get; }
    GameSource Source { get; }

    Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default);
}
