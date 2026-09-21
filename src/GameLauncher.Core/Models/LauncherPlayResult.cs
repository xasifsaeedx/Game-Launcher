namespace GameLauncher.Core.Models;

public sealed record LauncherPlayResult(
    PlaySession Session,
    string? GraphicsWarning,
    string? HatchableWarning);
