using Microsoft.Win32;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Adapters;

public sealed class GogGameSourceAdapter : IGameSourceAdapter
{
    public string Id => "gog";
    public string DisplayName => "GOG";
    public GameSource Source => GameSource.Gog;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var games = new List<DiscoveredGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var (productId, key) in RegistryAdapterUtilities.EnumerateSubKeys(
                         RegistryHive.LocalMachine,
                         view,
                         @"SOFTWARE\GOG.com\Games"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seen.Add(productId)) continue;

                var title = RegistryAdapterUtilities.ReadString(key, "gameName", "GameName");
                var installPath = RegistryAdapterUtilities.ReadString(key, "path", "Path");
                if (!GameDiscoveryUtilities.IsLikelyGameTitle(title) ||
                    string.IsNullOrWhiteSpace(installPath) ||
                    !Directory.Exists(installPath))
                {
                    continue;
                }

                var executable = GameDiscoveryUtilities.CleanExecutablePath(
                    RegistryAdapterUtilities.ReadString(key, "exe", "Exe"),
                    installPath)
                    ?? GameDiscoveryUtilities.FindBestExecutable(installPath, title!);

                var now = DateTimeOffset.UtcNow;
                var gameId = StableId.FromText($"gog-game:{productId}");
                var installId = StableId.FromText($"gog-install:{productId}");

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
                        productId,
                        installPath,
                        executable,
                        null,
                        true)));
            }
        }

        return Task.FromResult<IReadOnlyList<DiscoveredGame>>(games);
    }
}
