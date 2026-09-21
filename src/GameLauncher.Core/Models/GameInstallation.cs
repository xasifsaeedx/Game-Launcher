namespace GameLauncher.Core.Models;

public sealed record GameInstallation(
    Guid Id,
    Guid GameId,
    GameSource Source,
    string ExternalId,
    string? InstallPath,
    string? ExecutablePath,
    string? LaunchUri,
    bool IsInstalled);
