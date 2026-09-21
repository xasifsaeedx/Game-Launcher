using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface IGameRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertGameAsync(Game game, CancellationToken cancellationToken = default);

    Task UpsertInstallationAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameInstallation>> GetInstallationsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);
}
