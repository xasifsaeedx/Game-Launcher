using GameLauncher.Core.Models;

namespace GameLauncher.Core.Services;

public sealed class LauncherStatsService
{
    private readonly GameLibraryService _library;
    private readonly GameHistoryService _history;
    private readonly GamePreferenceService _preferences;
    private readonly PersonalLibraryService _personalLibrary;

    public LauncherStatsService(
        GameLibraryService library,
        GameHistoryService history,
        GamePreferenceService preferences,
        PersonalLibraryService personalLibrary)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _personalLibrary = personalLibrary ?? throw new ArgumentNullException(nameof(personalLibrary));
    }

    public async Task<LauncherStats> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var library = await _library.GetLibraryAsync(cancellationToken);
        var history = await _history.GetHistoryAsync(cancellationToken);
        var favoriteKeys = await _preferences.GetFavoriteKeysAsync(cancellationToken);
        var remote = await _personalLibrary.GetCachedGamesAsync(cancellationToken);

        var mostPlayed = library
            .OrderByDescending(x => x.TotalPlaytimeSeconds)
            .FirstOrDefault();

        var recent = library
            .Where(x => x.LastPlayedUtc.HasValue)
            .OrderByDescending(x => x.LastPlayedUtc)
            .FirstOrDefault();

        var nextUp = remote.Count(x =>
            string.Equals(x.LibraryStatus, "next", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.ProgressStatus, "playing", StringComparison.OrdinalIgnoreCase) ||
            x.Rank.HasValue);

        return new LauncherStats(
            library.Count,
            library.Count(x =>
                favoriteKeys.Contains(GamePreferenceService.GetGameKey(x.Game.Title))),
            nextUp,
            history.Count,
            library.Sum(x => x.TotalPlaytimeSeconds),
            history.Sum(x => x.KnownAccountPlaytimeSeconds),
            mostPlayed?.Game.Title,
            mostPlayed?.TotalPlaytimeSeconds ?? 0,
            recent?.Game.Title,
            recent?.LastPlayedUtc);
    }
}
