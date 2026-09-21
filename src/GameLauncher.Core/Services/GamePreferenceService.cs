using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public sealed class GamePreferenceService
{
    private readonly IGamePreferenceRepository _repository;

    public GamePreferenceService(IGamePreferenceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public static string GetGameKey(string title) =>
        GameTitleNormalizer.Normalize(title);

    public Task<GamePreference?> GetAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default) =>
        _repository.GetAsync(GetGameKey(item.Game.Title), cancellationToken);

    public async Task<bool> IsFavoriteAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default) =>
        (await GetAsync(item, cancellationToken))?.IsFavorite == true;

    public async Task<bool> ToggleFavoriteAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        var key = GetGameKey(item.Game.Title);
        var current = await _repository.GetAsync(key, cancellationToken);
        var next = current?.IsFavorite != true;
        await _repository.SetFavoriteAsync(key, next, cancellationToken);
        return next;
    }

    public Task SetFavoriteAsync(
        GameLibraryItem item,
        bool isFavorite,
        CancellationToken cancellationToken = default) =>
        _repository.SetFavoriteAsync(
            GetGameKey(item.Game.Title),
            isFavorite,
            cancellationToken);

    public async Task<IReadOnlySet<string>> GetFavoriteKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all
            .Where(x => x.IsFavorite)
            .Select(x => x.GameKey)
            .ToHashSet(StringComparer.Ordinal);
    }
}
