using GameLauncher.Core.Models;

namespace GameLauncher.App;

public sealed record InstallationOption(Guid? Id, string Label)
{
    public static InstallationOption Automatic { get; } = new(null, "Automatic / preferred");

    public static InstallationOption FromInstallation(GameInstallation installation) =>
        new(
            installation.Id,
            $"{DisplaySource(installation.Source)} — {installation.InstallPath ?? installation.ExternalId}");

    private static string DisplaySource(GameSource source) => source switch
    {
        GameSource.Gog => "GOG",
        GameSource.EA => "EA",
        GameSource.BattleNet => "Battle.net",
        GameSource.Xbox => "Xbox",
        _ => source.ToString()
    };
}
