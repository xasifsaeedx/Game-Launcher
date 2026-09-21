namespace GameLauncher.Core.Models;

public sealed record HatchableSyncResult(
    int RemoteCount,
    int MatchedCount,
    int PushedCount,
    int UnmatchedCount,
    IReadOnlyList<string> Warnings);
