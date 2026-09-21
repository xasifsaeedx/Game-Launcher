using System.Collections.Concurrent;
using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Runtime;

namespace GameLauncher.Core.Services;

public sealed class GameSessionService
{
    private readonly IGameRepository _repository;
    private readonly IGameRuntime _runtime;
    private readonly ConcurrentDictionary<Guid, byte> _runningGames = new();

    public GameSessionService(IGameRepository repository, IGameRuntime runtime)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public bool IsRunning(Guid gameId) => _runningGames.ContainsKey(gameId);

    public async Task<PlaySession> LaunchAndTrackAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);

        if (!_runningGames.TryAdd(installation.GameId, 0))
        {
            throw new InvalidOperationException("This game is already being tracked as running.");
        }

        try
        {
            await _repository.InitializeAsync(cancellationToken);
            await using var handle = await _runtime.LaunchAsync(installation, cancellationToken);

            var session = new PlaySession(
                Guid.NewGuid(),
                installation.GameId,
                handle.StartedUtc,
                null,
                null);

            await _repository.AddPlaySessionAsync(session, cancellationToken);

            if (!string.IsNullOrWhiteSpace(handle.DetectedExecutablePath) &&
                !string.Equals(
                    installation.ExecutablePath,
                    handle.DetectedExecutablePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                installation = installation with { ExecutablePath = handle.DetectedExecutablePath };
                await _repository.UpsertInstallationAsync(installation, cancellationToken);
            }

            DateTimeOffset endedUtc;
            try
            {
                endedUtc = await handle.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                endedUtc = DateTimeOffset.UtcNow;
            }

            var durationSeconds = Math.Max(
                0,
                (long)Math.Round((endedUtc - handle.StartedUtc).TotalSeconds));

            await _repository.EndPlaySessionAsync(
                session.Id,
                endedUtc,
                durationSeconds,
                CancellationToken.None);

            return session with
            {
                EndedUtc = endedUtc,
                DurationSeconds = durationSeconds
            };
        }
        finally
        {
            _runningGames.TryRemove(installation.GameId, out _);
        }
    }
}
