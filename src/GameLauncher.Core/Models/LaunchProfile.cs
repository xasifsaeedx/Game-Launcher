namespace GameLauncher.Core.Models;

public sealed record LaunchProfile(
    Guid Id,
    Guid GameId,
    string Name,
    Guid? InstallationId,
    string? GameArgumentsOverride,
    bool IsDefault,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
