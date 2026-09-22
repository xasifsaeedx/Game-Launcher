using GameLauncher.Core.Models;

namespace GameLauncher.App;

public sealed class GameCardViewModel
{
    public GameCardViewModel(
        GameLibraryItem item,
        PersonalLibraryGame? personalLibrary = null,
        GamePreference? preference = null)
    {
        Item = item;
        GameId = item.Game.Id;
        Title = item.Game.Title;
        CoverImagePath = item.Game.CoverImagePath;
        Installation = item.PreferredInstallation;

        Source = string.Join(
            " + ",
            item.Installations
                .Select(x => DisplaySource(x.Source))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        Playtime = FormatPlaytime(item.TotalPlaytimeSeconds);
        PersonalLibrary = personalLibrary;
        IsFavorite = preference?.IsFavorite == true;
        FavoriteGlyph = IsFavorite ? "★" : "☆";
        Rating = preference?.Rating ?? personalLibrary?.SheetRating;
        RatingText = Rating.HasValue ? $"{Rating}/10" : "Rate";
        RankBadge = personalLibrary?.Rank is int rank ? $"NEXT #{rank}" : string.Empty;
        SyncState = personalLibrary is null
            ? string.Empty
            : FormatLibraryState(personalLibrary);
    }

    public GameLibraryItem Item { get; }
    public Guid GameId { get; }
    public string Title { get; }
    public string? CoverImagePath { get; }
    public GameInstallation? Installation { get; }
    public string Source { get; }
    public string Playtime { get; }
    public PersonalLibraryGame? PersonalLibrary { get; }
    public bool IsFavorite { get; }
    public string FavoriteGlyph { get; }
    public int? Rating { get; }
    public string RatingText { get; }
    public string RankBadge { get; }
    public string SyncState { get; }

    public static string FormatPlaytime(long seconds)
    {
        if (seconds < 60) return "0 min";
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours < 1) return $"{Math.Max(1, (int)span.TotalMinutes)} min";
        return $"{span.TotalHours:0.#} h";
    }

    private static string FormatLibraryState(PersonalLibraryGame game)
    {
        if (!string.IsNullOrWhiteSpace(game.ProgressStatus))
        {
            return game.ProgressStatus switch
            {
                "playing" => "Playing",
                "completed" => "Completed",
                "paused" => "Paused",
                "dropped" => "Dropped",
                _ => ToTitleCase(game.ProgressStatus)
            };
        }

        return game.LibraryStatus switch
        {
            "next" => "Play next",
            "played" => "Played",
            "dislike" => "Didn't like",
            _ => string.Empty
        };
    }

    private static string ToTitleCase(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static string DisplaySource(GameSource source) => source switch
    {
        GameSource.Gog => "GOG",
        GameSource.EA => "EA",
        GameSource.BattleNet => "Battle.net",
        GameSource.MicrosoftStore => "Microsoft Store",
        GameSource.Xbox => "Xbox",
        _ => source.ToString()
    };
}
