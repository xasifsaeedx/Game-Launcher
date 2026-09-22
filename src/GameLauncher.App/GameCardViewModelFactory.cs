using GameLauncher.Core.Models;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

internal static class GameCardViewModelFactory
{
    public static GameCardViewModel[] Create(
        IReadOnlyList<GameLibraryItem> items,
        IReadOnlyList<PersonalLibraryGame> personalGames,
        IReadOnlyDictionary<string, GamePreference> preferences)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(personalGames);
        ArgumentNullException.ThrowIfNull(preferences);

        return items
            .Select(item =>
            {
                var personalGame = PersonalLibraryMatcher.FindMatch(
                    item,
                    personalGames);

                preferences.TryGetValue(
                    GamePreferenceService.GetGameKey(item.Game.Title),
                    out var preference);

                return new GameCardViewModel(
                    item,
                    personalGame,
                    preference);
            })
            .ToArray();
    }
}
