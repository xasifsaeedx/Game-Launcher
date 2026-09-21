namespace GameLauncher.Core.Runtime;

public interface IGameRunHandle : IAsyncDisposable
{
    DateTimeOffset StartedUtc { get; }
    int? ProcessId { get; }
    string? DetectedExecutablePath { get; }

    Task<DateTimeOffset> WaitForExitAsync(CancellationToken cancellationToken = default);
}
