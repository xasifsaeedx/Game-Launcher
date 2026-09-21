using Microsoft.Win32;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Adapters;

public sealed class EaGameSourceAdapter : IGameSourceAdapter
{
    public string Id => "ea";
    public string DisplayName => "EA app";
    public GameSource Source => GameSource.EA;

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
                         @"SOFTWARE\EA Games"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var title = RegistryAdapterUtilities.ReadString(key, "DisplayName") ?? subKeyName;
                var installPath = RegistryAdapterUtilities.ReadString(
                    key,
                    "Install Dir",
                    "InstallDir",
                    "Install Dir 64",
                    "InstallDir64");

                if (!GameDiscoveryUtilities.IsLikelyGameTitle(title) ||
                    string.IsNullOrWhiteSpace(installPath) ||
                    !Directory.Exists(installPath))
                {
                    continue;
                }

                var externalId = RegistryAdapterUtilities.ReadString(
                    key,
                    "Product GUID",
                    "ProductGUID",
                    "ContentID") ?? subKeyName;

                if (!seen.Add(externalId)) continue;

                var executable = GameDiscoveryUtilities.FindBestExecutable(installPath, title);
                var now = DateTimeOffset.UtcNow;
                var gameId = StableId.FromText($"ea-game:{externalId}");
                var installId = StableId.FromText($"ea-install:{externalId}");

                games.Add(new DiscoveredGame(
                    new Game(
                        gameId,
                        title,
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
