using GameLauncher.Core.Models;

namespace GameLauncher.Core.Repositories;

public interface IGameRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertGameAsync(Game game, CancellationToken cancellationToken = default);
    Task<Game?> GetGameAsync(Guid gameId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Game>> GetGamesAsync(CancellationToken cancellationToken = default);

    Task UpsertInstallationAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameInstallation>> GetInstallationsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task MarkSourceInstallationsNotInstalledAsync(
        GameSource source,
        CancellationToken cancellationToken = default);

    Task AddPlaySessionAsync(PlaySession session, CancellationToken cancellationToken = default);

    Task EndPlaySessionAsync(
        Guid sessionId,
        DateTimeOffset endedUtc,
        long durationSeconds,
        CancellationToken cancellationToken = default);

    Task<long> GetTotalPlaytimeSecondsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastPlayedUtcAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);
}
