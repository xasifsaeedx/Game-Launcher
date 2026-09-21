using Microsoft.Win32;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Adapters;

public sealed class BattleNetGameSourceAdapter : IGameSourceAdapter
{
    public string Id => "battlenet";
    public string DisplayName => "Battle.net";
    public GameSource Source => GameSource.BattleNet;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var games = new List<DiscoveredGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var (subKeyName, key) in RegistryAdapterUtilities.EnumerateSubKeys(
                         RegistryHive.LocalMachine,
                         view,
                         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var publisher = RegistryAdapterUtilities.ReadString(key, "Publisher") ?? string.Empty;
                if (!publisher.Contains("Blizzard", StringComparison.OrdinalIgnoreCase) &&
                    !publisher.Contains("Activision", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var title = RegistryAdapterUtilities.ReadString(key, "DisplayName");
                var installPath = RegistryAdapterUtilities.ReadString(key, "InstallLocation");

                if (!GameDiscoveryUtilities.IsLikelyGameTitle(title) ||
                    string.IsNullOrWhiteSpace(installPath) ||
                    !Directory.Exists(installPath))
                {
                    continue;
                }

                var externalId = subKeyName;
                if (!seen.Add(externalId)) continue;

                var displayIcon = GameDiscoveryUtilities.CleanExecutablePath(
                    RegistryAdapterUtilities.ReadString(key, "DisplayIcon"),
                    installPath);

                var executable = displayIcon is not null && File.Exists(displayIcon)
                    ? displayIcon
                    : GameDiscoveryUtilities.FindBestExecutable(installPath, title!);

                var now = DateTimeOffset.UtcNow;
                var gameId = StableId.FromText($"battlenet-game:{externalId}");
                var installId = StableId.FromText($"battlenet-install:{externalId}");

                games.Add(new DiscoveredGame(
                    new Game(
                        gameId,
                        title!,
                        now,
                        now,
                        GameDiscoveryUtilities.FindLocalArtwork(installPath)),
                    new GameInstallation(
                        installId,
                        gameId,
                        Source,
                        externalId,
                        installPath,
                        executable,
                        null,
                        true)));
            }
        }

        return Task.FromResult<IReadOnlyList<DiscoveredGame>>(games);
    }
}
