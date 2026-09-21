using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Adapters;

public sealed class XboxGameSourceAdapter : IGameSourceAdapter
{
    private readonly IReadOnlyList<string>? _driveRoots;

    public XboxGameSourceAdapter(IEnumerable<string>? driveRoots = null)
    {
        _driveRoots = driveRoots?.ToArray();
    }

    public string Id => "xbox";
    public string DisplayName => "Xbox / Microsoft Store";
    public GameSource Source => GameSource.Xbox;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var games = new List<DiscoveredGame>();

        foreach (var driveRoot in GetDriveRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var xboxRoot = Path.Combine(driveRoot, "XboxGames");
            if (!Directory.Exists(xboxRoot)) continue;

            IEnumerable<string> gameFolders;
            try
            {
                gameFolders = Directory.EnumerateDirectories(xboxRoot).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var folder in gameFolders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var content = Path.Combine(folder, "Content");
                var helper = Path.Combine(content, "gamelaunchhelper.exe");
                if (!File.Exists(helper)) continue;

                var title = Path.GetFileName(folder);
                if (string.IsNullOrWhiteSpace(title)) continue;

                var externalId = Path.GetFullPath(folder);
                var now = DateTimeOffset.UtcNow;
                var gameId = StableId.FromText($"xbox-game:{externalId}");
                var installId = StableId.FromText($"xbox-install:{externalId}");

                var game = new Game(
                    gameId,
                    title,
                    now,
                    now,
                    GameDiscoveryUtilities.FindLocalArtwork(content) ??
                    GameDiscoveryUtilities.FindLocalArtwork(folder));

                var installation = new GameInstallation(
                    installId,
                    gameId,
                    Source,
                    externalId,
                    content,
                    helper,
                    null,
                    true);

                games.Add(new DiscoveredGame(game, installation));
            }
        }

        return Task.FromResult<IReadOnlyList<DiscoveredGame>>(games);
    }

    private IEnumerable<string> GetDriveRoots()
    {
        if (_driveRoots is not null) return _driveRoots;

        return DriveInfo.GetDrives()
            .Where(x => x.IsReady && x.DriveType is DriveType.Fixed or DriveType.Removable)
            .Select(x => x.RootDirectory.FullName)
            .ToArray();
    }
}
