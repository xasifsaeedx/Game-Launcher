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

    public async Task<SourceSyncResult> SyncSourcesAsync(CancellationToken cancellationToken = default)
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

        return new SourceSyncResult(_adapters.Count, discoveredCount, storedCount);
    }

    public async Task<GameLibraryItem> AddManualGameAsync(
        string title,
        string executablePath,
        string? coverImagePath = null,
        string? launchArguments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        await _repository.InitializeAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var gameId = Guid.NewGuid();
        var game = new Game(
            gameId,
            title.Trim(),
            now,
            now,
            NormalizeOptional(coverImagePath));

        var executable = Path.GetFullPath(executablePath.Trim());
        var installation = new GameInstallation(
            Guid.NewGuid(),
            gameId,
            GameSource.Manual,
            gameId.ToString("D"),
            Path.GetDirectoryName(executable),
            executable,
            null,
            true,
            NormalizeOptional(launchArguments));

        await _repository.UpsertGameAsync(game, cancellationToken);
        await _repository.UpsertInstallationAsync(installation, cancellationToken);

        return new GameLibraryItem(game, new[] { installation }, 0, null);
    }

    public async Task<IReadOnlyList<GameLibraryItem>> GetLibraryAsync(
        CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);
        var games = await _repository.GetGamesAsync(cancellationToken);
        var items = new List<GameLibraryItem>(games.Count);

        foreach (var game in games)
        {
            var installations = await _repository.GetInstallationsAsync(game.Id, cancellationToken);
            var playtime = await _repository.GetTotalPlaytimeSecondsAsync(game.Id, cancellationToken);
            var lastPlayed = await _repository.GetLastPlayedUtcAsync(game.Id, cancellationToken);
            items.Add(new GameLibraryItem(game, installations, playtime, lastPlayed));
        }

        return items;
    }

    public async Task<GameLibraryItem?> GetLibraryItemAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);
        var game = await _repository.GetGameAsync(gameId, cancellationToken);
        if (game is null) return null;

        var installations = await _repository.GetInstallationsAsync(gameId, cancellationToken);
        var playtime = await _repository.GetTotalPlaytimeSecondsAsync(gameId, cancellationToken);
        var lastPlayed = await _repository.GetLastPlayedUtcAsync(gameId, cancellationToken);
        return new GameLibraryItem(game, installations, playtime, lastPlayed);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
