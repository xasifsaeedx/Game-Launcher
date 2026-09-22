using GameLauncher.Core.Models;

namespace GameLauncher.Core.Sync;

public interface IPersonalLibraryClient
{
    Task<IReadOnlyList<PersonalLibraryGame>> GetGamesAsync(
        PersonalLibrarySettings settings,
        CancellationToken cancellationToken = default);
}
