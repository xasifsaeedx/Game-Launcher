using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface ILaunchProfileRepository
{
    Task<IReadOnlyList<LaunchProfile>> GetLaunchProfilesAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task<LaunchProfile?> GetLaunchProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LaunchAction>> GetLaunchActionsAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task UpsertLaunchProfileAsync(
        LaunchProfile profile,
        CancellationToken cancellationToken = default);

    Task ReplaceLaunchActionsAsync(
        Guid profileId,
        IReadOnlyList<LaunchAction> actions,
        CancellationToken cancellationToken = default);

    Task DeleteLaunchProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task ClearDefaultLaunchProfilesAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);
}
