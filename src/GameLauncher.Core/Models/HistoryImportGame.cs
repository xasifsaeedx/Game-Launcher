namespace GameLauncher.Core.Models;

public sealed record HistoryImportGame(
    GameSource Source,
    string ExternalId,
    string Title,
    string? Platform,
    long? PlaytimeSeconds,
    DateTimeOffset? LastPlayedUtc);
