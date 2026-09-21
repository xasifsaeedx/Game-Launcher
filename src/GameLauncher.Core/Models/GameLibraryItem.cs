namespace GameLauncher.Core.Models;

public sealed record GameLibraryItem(
    Game Game,
    IReadOnlyList<GameInstallation> Installations,
    long TotalPlaytimeSeconds,
    DateTimeOffset? LastPlayedUtc)
{
    public GameInstallation? PreferredInstallation =>
        Installations.FirstOrDefault(x => x.IsInstalled) ?? Installations.FirstOrDefault();
}
