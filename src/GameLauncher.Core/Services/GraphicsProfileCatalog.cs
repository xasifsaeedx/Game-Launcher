using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public sealed class GraphicsProfileCatalog
{
    private readonly IReadOnlyList<GameGraphicsProfile> _profiles;

    public GraphicsProfileCatalog(IEnumerable<GameGraphicsProfile> profiles)
    {
        _profiles = profiles?.ToArray() ?? throw new ArgumentNullException(nameof(profiles));
    }

    public IReadOnlyList<GameGraphicsProfile> Profiles => _profiles;

    public GameGraphicsProfile? FindMatch(GameLibraryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var steamIds = item.Installations
            .Where(x => x.Source == GameSource.Steam)
            .Select(x => x.ExternalId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var steamMatch = _profiles.FirstOrDefault(profile =>
            profile.SteamAppId.HasValue &&
            steamIds.Contains(profile.SteamAppId.Value.ToString()));
        if (steamMatch is not null) return steamMatch;

        var normalized = GameTitleNormalizer.Normalize(item.Game.Title);
        return _profiles.FirstOrDefault(profile =>
            string.Equals(
                GameTitleNormalizer.Normalize(profile.Title),
                normalized,
                StringComparison.Ordinal) ||
            profile.AlternateTitles.Any(title =>
                string.Equals(
                    GameTitleNormalizer.Normalize(title),
                    normalized,
                    StringComparison.Ordinal)));
    }
}
