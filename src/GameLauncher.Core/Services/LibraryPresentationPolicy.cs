using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public static class LibraryPresentationPolicy
{
    public static bool Matches(
        GameLibraryItem item,
        HatchableRemoteGame? hatchable,
        bool isFavorite,
        string? search,
        LibraryFilterMode filter)
    {
        ArgumentNullException.ThrowIfNull(item);

        var normalizedSearch = GameTitleNormalizer.Normalize(search ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var haystack = string.Join(
                " ",
                item.Game.Title,
                string.Join(" ", item.Installations.Select(x => x.Source.ToString())),
                hatchable?.Title ?? string.Empty,
                hatchable?.Platforms ?? string.Empty);

            if (!GameTitleNormalizer.Normalize(haystack)
                    .Contains(normalizedSearch, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return filter switch
        {
            LibraryFilterMode.Favorites => isFavorite,
            LibraryFilterMode.NextUp =>
                string.Equals(hatchable?.LibraryStatus, "next", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hatchable?.ProgressStatus, "playing", StringComparison.OrdinalIgnoreCase),
            LibraryFilterMode.Playing =>
                string.Equals(hatchable?.ProgressStatus, "playing", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    public static int NextUpSortKey(HatchableRemoteGame? hatchable) =>
        string.Equals(hatchable?.ProgressStatus, "playing", StringComparison.OrdinalIgnoreCase)
            ? int.MinValue
            : hatchable?.RankScore ?? int.MaxValue;
}
