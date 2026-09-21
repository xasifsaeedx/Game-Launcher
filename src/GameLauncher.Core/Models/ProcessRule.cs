namespace GameLauncher.Core.Models;

public sealed record ProcessRule(
    Guid Id,
    Guid GameId,
    string ProcessName,
    string? ExecutablePath,
    ProcessMatchType MatchType,
    bool IsPrimary);
