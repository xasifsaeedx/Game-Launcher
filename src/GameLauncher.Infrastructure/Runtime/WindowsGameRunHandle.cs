using GameLauncher.Core.Runtime;

namespace GameLauncher.Infrastructure.Runtime;

internal sealed class WindowsGameRunHandle : IGameRunHandle
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(500);

    private readonly string? _expectedExecutableName;

    public WindowsGameRunHandle(
        int processId,
        DateTimeOffset startedUtc,
        string? detectedExecutablePath,
        string? expectedExecutableName)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processId));
        }

        ProcessId = processId;
        StartedUtc = startedUtc;
        DetectedExecutablePath = detectedExecutablePath;
        _expectedExecutableName = expectedExecutableName;
    }

    public DateTimeOffset StartedUtc { get; }
    public int? ProcessId { get; }
    public string? DetectedExecutablePath { get; }

    public async Task<DateTimeOffset> WaitForExitAsync(
        CancellationToken cancellationToken = default)
    {
        if (!ProcessId.HasValue)
        {
            return DateTimeOffset.UtcNow;
        }

        while (WindowsProcessSnapshot.IsAlive(
                   ProcessId.Value,
                   _expectedExecutableName))
        {
            await Task.Delay(
                PollInterval,
                cancellationToken);
        }

        return DateTimeOffset.UtcNow;
    }

    public ValueTask DisposeAsync() =>
        ValueTask.CompletedTask;
}
