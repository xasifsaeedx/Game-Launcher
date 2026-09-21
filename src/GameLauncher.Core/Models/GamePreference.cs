namespace GameLauncher.Core.Models;

public sealed record GamePreference(
    string GameKey,
    bool IsFavorite,
    DateTimeOffset UpdatedUtc);
