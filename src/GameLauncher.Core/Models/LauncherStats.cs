namespace GameLauncher.Core.Models;

public sealed record LauncherStats(
    int InstalledGames,
    int FavoriteGames,
    int NextUpGames,
    int HistoricalGames,
    long LauncherTrackedSeconds,
    long KnownAccountPlaytimeSeconds,
    string? MostPlayedGame,
    long MostPlayedSeconds,
    string? RecentlyPlayedGame,
    DateTimeOffset? RecentlyPlayedUtc);
