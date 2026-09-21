using GameLauncher.Core.Models;

namespace GameLauncher.Core.History;

public interface ISteamHistoryClient
{
    Task<IReadOnlyList<HistoryImportGame>> GetOwnedGamesAsync(
        SteamHistorySettings settings,
        CancellationToken cancellationToken = default);
}
