namespace GameLauncher.Core.Models;

public sealed record GamePreference(
    string GameKey,
    bool IsFavorite,
    int? Rating,
    DateTimeOffset UpdatedUtc);
