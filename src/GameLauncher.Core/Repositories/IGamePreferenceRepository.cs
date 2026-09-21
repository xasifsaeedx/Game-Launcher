using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface IGamePreferenceRepository
{
    Task<GamePreference?> GetAsync(
        string gameKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GamePreference>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(
        string gameKey,
        bool isFavorite,
        CancellationToken cancellationToken = default);
}
