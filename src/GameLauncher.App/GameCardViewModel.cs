using GameLauncher.Core.Models;

namespace GameLauncher.App;

public sealed class GameCardViewModel
{
    public GameCardViewModel(GameLibraryItem item)
    {
        Item = item;
        GameId = item.Game.Id;
        Title = item.Game.Title;
        CoverImagePath = item.Game.CoverImagePath;
        Installation = item.PreferredInstallation;
        Source = Installation?.Source.ToString() ?? "Unknown";
        Playtime = FormatPlaytime(item.TotalPlaytimeSeconds);
    }

    public GameLibraryItem Item { get; }
    public Guid GameId { get; }
    public string Title { get; }
    public string? CoverImagePath { get; }
    public GameInstallation? Installation { get; }
    public string Source { get; }
    public string Playtime { get; }

    public static string FormatPlaytime(long seconds)
    {
        if (seconds < 60) return "0 min";
        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours < 1) return $"{Math.Max(1, (int)span.TotalMinutes)} min";
        return $"{span.TotalHours:0.#} h";
    }
}
