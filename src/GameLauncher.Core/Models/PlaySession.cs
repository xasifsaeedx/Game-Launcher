namespace GameLauncher.Core.Models;

public sealed record PlaySession(
    Guid Id,
    Guid GameId,
    DateTimeOffset StartedUtc,
    DateTimeOffset? EndedUtc,
    long? DurationSeconds);
