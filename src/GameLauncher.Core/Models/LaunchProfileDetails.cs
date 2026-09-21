namespace GameLauncher.Core.Models;

public sealed record LaunchProfileDetails(
    LaunchProfile Profile,
    IReadOnlyList<LaunchAction> Actions);
