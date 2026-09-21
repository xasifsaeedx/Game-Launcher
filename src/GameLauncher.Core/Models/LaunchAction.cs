namespace GameLauncher.Core.Models;

public sealed record LaunchAction(
    Guid Id,
    Guid ProfileId,
    LaunchActionStage Stage,
    string Name,
    string ExecutablePath,
    string? Arguments,
    string? WorkingDirectory,
    int SortOrder,
    bool WaitForExit,
    bool CloseWithGame,
    bool IsEnabled);
