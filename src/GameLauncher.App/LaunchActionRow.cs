using GameLauncher.Core.Models;

namespace GameLauncher.App;

public sealed class LaunchActionRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LaunchActionStage Stage { get; set; } = LaunchActionStage.Companion;
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public int SortOrder { get; set; }
    public bool WaitForExit { get; set; }
    public bool CloseWithGame { get; set; } = true;
    public bool IsEnabled { get; set; } = true;

    public static LaunchActionRow FromModel(LaunchAction action) =>
        new()
        {
            Id = action.Id,
            Stage = action.Stage,
            Name = action.Name,
            ExecutablePath = action.ExecutablePath,
            Arguments = action.Arguments,
            WorkingDirectory = action.WorkingDirectory,
            SortOrder = action.SortOrder,
            WaitForExit = action.WaitForExit,
            CloseWithGame = action.CloseWithGame,
            IsEnabled = action.IsEnabled
        };

    public LaunchAction ToModel(Guid profileId) =>
        new(
            Id,
            profileId,
            Stage,
            Name,
            ExecutablePath,
            Arguments,
            WorkingDirectory,
            SortOrder,
            WaitForExit,
            CloseWithGame,
            IsEnabled);
}
