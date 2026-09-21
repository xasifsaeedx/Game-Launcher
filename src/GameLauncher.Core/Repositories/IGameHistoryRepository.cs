using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface IGameHistoryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(
        GameHistoryRecord record,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameHistoryRecord>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameHistoryRecord>> GetBySourceAsync(
        GameSource source,
        CancellationToken cancellationToken = default);
}
