using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Adapters;

public sealed class SteamGameSourceAdapter : IGameSourceAdapter
{
    public string Id => "steam";
    public string DisplayName => "Steam";
    public GameSource Source => GameSource.Steam;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var discovered = new Dictionary<string, DiscoveredGame>(StringComparer.OrdinalIgnoreCase);

        foreach (var steamRoot in SteamLibraryLocator.FindSteamRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var library in SteamLibraryLocator.FindLibraries(steamRoot))
            {
                cancellationToken.ThrowIfCancellationRequested();
                DiscoverLibrary(library, discovered, cancellationToken);
            }
        }

        IReadOnlyList<DiscoveredGame> result = discovered.Values
            .OrderBy(x => x.Game.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(result);
    }

    private static void DiscoverLibrary(
        string library,
        IDictionary<string, DiscoveredGame> discovered,
        CancellationToken cancellationToken)
    {
        var steamApps = Path.Combine(library, "steamapps");
        if (!Directory.Exists(steamApps)) return;

        IEnumerable<string> manifestFiles;
        try
        {
            manifestFiles = Directory.EnumerateFiles(steamApps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return;
        }

        foreach (var manifestFile in manifestFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var manifest = SteamManifestParser.Parse(File.ReadAllText(manifestFile));
                if (manifest is null || discovered.ContainsKey(manifest.AppId)) continue;

                var installPath = Path.Combine(steamApps, "common", manifest.InstallDirectoryName);
                if (!Directory.Exists(installPath)) continue;

                var now = DateTimeOffset.UtcNow;
                var gameId = StableId.FromText($"steam-game:{manifest.AppId}");
                var installationId = StableId.FromText($"steam-install:{manifest.AppId}");

                var game = new Game(gameId, manifest.Name, now, now);
                var installation = new GameInstallation(
                    installationId,
                    gameId,
                    GameSource.Steam,
                    manifest.AppId,
                    installPath,
                    null,
                    $"steam://rungameid/{manifest.AppId}",
                    true);

                discovered[manifest.AppId] = new DiscoveredGame(game, installation);
            }
            catch
            {
                // A single bad manifest must not break discovery of the rest of the library.
            }
        }
    }
}
