namespace GameLauncher.Core.Models;

public sealed record PersonalLibraryGame(
    int SourceRow,
    string Title,
    string Platforms,
    long? SteamAppId,
    string? CoverUrl,
    int? Rank,
    string? LibraryStatus,
    string? ProgressStatus,
    int? SheetRating);
