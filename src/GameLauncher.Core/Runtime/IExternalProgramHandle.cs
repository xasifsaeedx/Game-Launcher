namespace GameLauncher.Core.Runtime;

public interface IExternalProgramHandle : IAsyncDisposable
{
    bool IsRunning { get; }

    Task WaitForExitAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
