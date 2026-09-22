using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public static class PersonalLibraryMatcher
{
    public static PersonalLibraryGame? FindMatch(
        GameLibraryItem localGame,
        IReadOnlyList<PersonalLibraryGame> personalGames)
    {
        ArgumentNullException.ThrowIfNull(localGame);
        ArgumentNullException.ThrowIfNull(personalGames);

        var steamIds = localGame.Installations
            .Where(x => x.Source == GameSource.Steam)
            .Select(x => x.ExternalId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var steamMatch = personalGames.FirstOrDefault(game =>
            game.SteamAppId.HasValue &&
            steamIds.Contains(game.SteamAppId.Value.ToString()));

        if (steamMatch is not null)
        {
            return steamMatch;
        }

        var normalizedTitle = GameTitleNormalizer.Normalize(
            localGame.Game.Title);

        return personalGames.FirstOrDefault(game =>
            string.Equals(
                GameTitleNormalizer.Normalize(game.Title),
                normalizedTitle,
                StringComparison.Ordinal));
    }
}
