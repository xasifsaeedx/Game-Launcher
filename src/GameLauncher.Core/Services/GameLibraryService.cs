using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;

namespace GameLauncher.Core.Services;

public sealed class GameLibraryService
{
    private readonly IGameRepository _repository;
    private readonly IReadOnlyList<IGameSourceAdapter> _adapters;

    public GameLibraryService(
        IGameRepository repository,
        IEnumerable<IGameSourceAdapter> adapters)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _adapters = adapters?.ToArray() ?? throw new ArgumentNullException(nameof(adapters));
    }

    public async Task<FoundationCheckResult> DiscoverAndStoreAsync(
        CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);

        var discoveredCount = 0;
        var storedCount = 0;

        foreach (var adapter in _adapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var discovered = await adapter.DiscoverInstalledGamesAsync(cancellationToken);
            discoveredCount += discovered.Count;

            foreach (var item in discovered)
            {
                if (item.Installation.GameId != item.Game.Id)
                {
                    throw new InvalidOperationException(
                        $"Adapter '{adapter.Id}' returned an installation linked to a different game ID.");
                }

                await _repository.UpsertGameAsync(item.Game, cancellationToken);
                await _repository.UpsertInstallationAsync(item.Installation, cancellationToken);
                storedCount++;
            }
        }

        return new FoundationCheckResult(_adapters.Count, discoveredCount, storedCount);
    }

    public async Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);
        return await _repository.GetGamesAsync(cancellationToken);
    }
}
