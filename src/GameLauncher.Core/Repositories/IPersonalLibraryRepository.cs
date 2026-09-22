using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface IPersonalLibraryRepository
{
    Task<PersonalLibrarySettings?> GetSettingsAsync(
        CancellationToken cancellationToken = default);

    Task SaveSettingsAsync(
        PersonalLibrarySettings settings,
        CancellationToken cancellationToken = default);

    Task ClearSettingsAsync(
        CancellationToken cancellationToken = default);

    Task ReplaceGamesAsync(
        IReadOnlyList<PersonalLibraryGame> games,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PersonalLibraryGame>> GetGamesAsync(
        CancellationToken cancellationToken = default);
}
