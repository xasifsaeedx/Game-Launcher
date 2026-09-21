using System.Diagnostics;
using GameLauncher.Core.Runtime;

namespace GameLauncher.Infrastructure.Runtime;

internal sealed class WindowsGameRunHandle : IGameRunHandle
{
    private readonly Process _process;

    public WindowsGameRunHandle(Process process, DateTimeOffset startedUtc, string? detectedExecutablePath)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        StartedUtc = startedUtc;
        DetectedExecutablePath = detectedExecutablePath;
    }

    public DateTimeOffset StartedUtc { get; }
    public string? DetectedExecutablePath { get; }

    public async Task<DateTimeOffset> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _process.WaitForExitAsync(cancellationToken);
            return DateTimeOffset.UtcNow;
        }
        catch (InvalidOperationException)
        {
            return DateTimeOffset.UtcNow;
        }
    }

    public ValueTask DisposeAsync()
    {
        _process.Dispose();
        return ValueTask.CompletedTask;
    }
}
