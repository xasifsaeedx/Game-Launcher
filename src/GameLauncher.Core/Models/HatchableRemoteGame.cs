namespace GameLauncher.Core.Models;

public sealed record HatchableRemoteGame(
    int RemoteGameId,
    string Title,
    string Platforms,
    long? SteamAppId,
    string? CoverUrl,
    int RankScore,
    string? LibraryStatus,
    string? ProgressStatus,
    int? Rating,
    long PlaytimeSeconds,
    DateTimeOffset? LastPlayedAt);
