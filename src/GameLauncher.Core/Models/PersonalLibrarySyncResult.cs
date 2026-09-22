namespace GameLauncher.Core.Models;

public sealed record PersonalLibrarySyncResult(
    int RemoteCount,
    int MatchedCount);
