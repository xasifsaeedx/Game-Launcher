using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public static class LibraryPresentationPolicy
{
    public static bool Matches(
        GameLibraryItem item,
        PersonalLibraryGame? personalLibrary,
        bool isFavorite,
        string? search,
        LibraryFilterMode filter)
    {
        ArgumentNullException.ThrowIfNull(item);

        var normalizedSearch = GameTitleNormalizer.Normalize(
            search ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var haystack = string.Join(
                " ",
                item.Game.Title,
                string.Join(
                    " ",
                    item.Installations.Select(x => x.Source.ToString())),
                personalLibrary?.Title ?? string.Empty,
                personalLibrary?.Platforms ?? string.Empty);

            if (!GameTitleNormalizer
                    .Normalize(haystack)
                    .Contains(normalizedSearch, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return filter switch
        {
            LibraryFilterMode.Favorites => isFavorite,
            LibraryFilterMode.NextUp =>
                IsStatus(personalLibrary?.LibraryStatus, "next") ||
                IsStatus(personalLibrary?.ProgressStatus, "playing") ||
                personalLibrary?.Rank is not null,
            LibraryFilterMode.Playing =>
                IsStatus(personalLibrary?.ProgressStatus, "playing"),
            _ => true
        };
    }

    public static int NextUpSortKey(
        PersonalLibraryGame? personalLibrary) =>
        IsStatus(personalLibrary?.ProgressStatus, "playing")
            ? int.MinValue
            : personalLibrary?.Rank ?? int.MaxValue;

    private static bool IsStatus(
        string? actual,
        string expected) =>
        string.Equals(
            actual,
            expected,
            StringComparison.OrdinalIgnoreCase);
}
