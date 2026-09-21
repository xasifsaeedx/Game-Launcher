using GameLauncher.Core.Models;

namespace GameLauncher.App;

public sealed record GamingHistoryViewModel(GamingHistoryItem Item)
{
    public string Title => Item.Title;
    public string Sources => Item.Sources.Count == 0
        ? "Launcher only"
        : string.Join(", ", Item.Sources.Select(DisplaySource));
    public string AccountPlaytime => FormatPlaytime(Item.KnownAccountPlaytimeSeconds);
    public string LauncherPlaytime => FormatPlaytime(Item.LauncherTrackedSeconds);
    public string LastPlayed => Item.LastPlayedUtc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "—";
    public string Installed => Item.IsInstalled ? "Installed" : string.Empty;

    private static string FormatPlaytime(long seconds)
    {
        if (seconds <= 0) return "—";
        var hours = seconds / 3600d;
        return hours >= 1
            ? $"{hours:0.#} h"
            : $"{Math.Max(1, seconds / 60)} min";
    }

    private static string DisplaySource(GameSource source) => source switch
    {
        GameSource.Gog => "GOG",
        GameSource.EA => "EA",
        GameSource.BattleNet => "Battle.net",
        GameSource.MicrosoftStore => "Microsoft Store",
        GameSource.PlayStation => "PlayStation",
        _ => source.ToString()
    };
}
