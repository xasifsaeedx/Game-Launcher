using GameLauncher.Core.Adapters;
using GameLauncher.Core.Metadata;
using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public sealed class GameLibraryService
{
    private readonly IGameRepository _repository;
    private readonly IReadOnlyList<IGameSourceAdapter> _adapters;
    private readonly IGameMetadataEnricher? _metadataEnricher;

    public GameLibraryService(
        IGameRepository repository,
        IEnumerable<IGameSourceAdapter> adapters,
        IGameMetadataEnricher? metadataEnricher = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _adapters = adapters?.ToArray() ?? throw new ArgumentNullException(nameof(adapters));
        _metadataEnricher = metadataEnricher;
    }

    public async Task<SourceSyncResult> SyncSourcesAsync(CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);

        var discoveredCount = 0;
        var storedCount = 0;
        var warnings = new List<string>();

        foreach (var adapter in _adapters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var discovered = await adapter.DiscoverInstalledGamesAsync(cancellationToken);
                discoveredCount += discovered.Count;

                await _repository.MarkSourceInstallationsNotInstalledAsync(
                    adapter.Source,
                    cancellationToken);

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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                warnings.Add($"{adapter.DisplayName}: {ex.Message}");
            }
        }

        var metadataUpdated = await EnrichMissingMetadataAsync(warnings, cancellationToken);

        return new SourceSyncResult(
            _adapters.Count,
            discoveredCount,
            storedCount,
            metadataUpdated,
            warnings);
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

        if (_metadataEnricher is not null && string.IsNullOrWhiteSpace(game.CoverImagePath))
        {
            try
            {
                var enriched = await _metadataEnricher.EnrichAsync(
                    game,
                    new[] { installation },
                    cancellationToken);

                if (!Equals(enriched, game))
                {
                    game = enriched;
                    await _repository.UpsertGameAsync(game, cancellationToken);
                }
            }
            catch
            {
                // Metadata is optional and must never prevent a manual game from being added.
            }
        }

        return new GameLibraryItem(game, new[] { installation }, 0, null);
    }

    public async Task<IReadOnlyList<GameLibraryItem>> GetLibraryAsync(
        CancellationToken cancellationToken = default)
    {
        await _repository.InitializeAsync(cancellationToken);
        var games = await _repository.GetGamesAsync(cancellationToken);
        var rawItems = new List<GameLibraryItem>(games.Count);

        foreach (var game in games)
        {
            var installations = await _repository.GetInstallationsAsync(game.Id, cancellationToken);
            var installed = installations.Where(x => x.IsInstalled).ToArray();
            if (installed.Length == 0) continue;

            var playtime = await _repository.GetTotalPlaytimeSecondsAsync(game.Id, cancellationToken);
            var lastPlayed = await _repository.GetLastPlayedUtcAsync(game.Id, cancellationToken);
            rawItems.Add(new GameLibraryItem(game, installed, playtime, lastPlayed));
        }

        return rawItems
            .GroupBy(x => GameTitleNormalizer.Normalize(x.Game.Title))
            .Select(MergeGroup)
            .OrderBy(x => x.Game.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<GameLibraryItem?> GetLibraryItemAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var library = await GetLibraryAsync(cancellationToken);
        return library.FirstOrDefault(item =>
            item.Game.Id == gameId ||
            item.Installations.Any(installation => installation.GameId == gameId));
    }

    private async Task<int> EnrichMissingMetadataAsync(
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        if (_metadataEnricher is null) return 0;

        var games = await _repository.GetGamesAsync(cancellationToken);
        var missing = games.Where(x => string.IsNullOrWhiteSpace(x.CoverImagePath)).ToArray();
        if (missing.Length == 0) return 0;

        var updates = new List<Game>();
        using var gate = new SemaphoreSlim(4);

        var tasks = missing.Select(async game =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var installations = await _repository.GetInstallationsAsync(game.Id, cancellationToken);
                var enriched = await _metadataEnricher.EnrichAsync(game, installations, cancellationToken);
                if (!string.Equals(
                        enriched.CoverImagePath,
                        game.CoverImagePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    lock (updates) updates.Add(enriched);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lock (warnings) warnings.Add($"Artwork for {game.Title}: {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        foreach (var game in updates)
        {
            await _repository.UpsertGameAsync(game, cancellationToken);
        }

        return updates.Count;
    }

    private static GameLibraryItem MergeGroup(IGrouping<string, GameLibraryItem> group)
    {
        var items = group.ToArray();
        var representative = items
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.Game.CoverImagePath))
            .ThenByDescending(x => x.Installations.Any(i => i.Source == GameSource.Manual))
            .ThenByDescending(x => x.Installations.Any(i => i.Source == GameSource.Steam))
            .First();

        var installations = items
            .SelectMany(x => x.Installations)
            .GroupBy(x => (x.Source, x.ExternalId), new SourceExternalIdComparer())
            .Select(x => x.First())
            .OrderBy(x => x.Source)
            .ToArray();

        return new GameLibraryItem(
            representative.Game,
            installations,
            items.Sum(x => x.TotalPlaytimeSeconds),
            items.Where(x => x.LastPlayedUtc.HasValue)
                .Select(x => x.LastPlayedUtc)
                .Max());
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class SourceExternalIdComparer :
        IEqualityComparer<(GameSource Source, string ExternalId)>
    {
        public bool Equals(
            (GameSource Source, string ExternalId) x,
            (GameSource Source, string ExternalId) y) =>
            x.Source == y.Source &&
            string.Equals(x.ExternalId, y.ExternalId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((GameSource Source, string ExternalId) obj) =>
            HashCode.Combine(obj.Source, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ExternalId));
    }
}
