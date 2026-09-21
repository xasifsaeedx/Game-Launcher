namespace GameLauncher.Core.Models;

public sealed record HatchableGamePush(
    int RemoteGameId,
    long PlaytimeSeconds,
    DateTimeOffset? LastPlayedAt,
    string? ProgressStatus,
    int? Rating);
