namespace GameLauncher.Core.Models;

public sealed record GameHistoryRecord(
    Guid Id,
    GameSource Source,
    string ExternalId,
    string Title,
    string? Platform,
    long? PlaytimeSeconds,
    DateTimeOffset? LastPlayedUtc,
    DateTimeOffset ImportedUtc);
