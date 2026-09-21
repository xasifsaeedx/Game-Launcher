using Microsoft.Win32;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Adapters;

public sealed class UbisoftGameSourceAdapter : IGameSourceAdapter
{
    public string Id => "ubisoft";
    public string DisplayName => "Ubisoft Connect";
    public GameSource Source => GameSource.Ubisoft;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var games = new List<DiscoveredGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var (gameIdText, key) in RegistryAdapterUtilities.EnumerateSubKeys(
                         RegistryHive.LocalMachine,
                         view,
                         @"SOFTWARE\Ubisoft\Launcher\Installs"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seen.Add(gameIdText)) continue;

                var installPath = RegistryAdapterUtilities.ReadString(key, "InstallDir", "InstallDirCache");
                if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath)) continue;

                var title = RegistryAdapterUtilities.FindUninstallDisplayName($"UPlay Install {gameIdText}")
                            ?? Path.GetFileName(
                                installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                if (!GameDiscoveryUtilities.IsLikelyGameTitle(title)) continue;

                var executable = GameDiscoveryUtilities.FindBestExecutable(installPath, title!);
                var now = DateTimeOffset.UtcNow;
                var gameId = StableId.FromText($"ubisoft-game:{gameIdText}");
                var installId = StableId.FromText($"ubisoft-install:{gameIdText}");

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
                        gameIdText,
                        installPath,
                        executable,
                        $"uplay://launch/{gameIdText}/0",
                        true)));
            }
        }

        return Task.FromResult<IReadOnlyList<DiscoveredGame>>(games);
    }
}
