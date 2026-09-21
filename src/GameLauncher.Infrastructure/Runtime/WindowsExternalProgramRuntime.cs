using System.Diagnostics;
using GameLauncher.Core.Models;
using GameLauncher.Core.Runtime;

namespace GameLauncher.Infrastructure.Runtime;

public sealed class WindowsExternalProgramRuntime : IExternalProgramRuntime
{
    public Task<IExternalProgramHandle> StartAsync(
        LaunchAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(action.ExecutablePath))
        {
            throw new FileNotFoundException(
                $"The configured program for '{action.Name}' does not exist.",
                action.ExecutablePath);
        }

        var workingDirectory = string.IsNullOrWhiteSpace(action.WorkingDirectory)
            ? Path.GetDirectoryName(action.ExecutablePath) ?? string.Empty
            : action.WorkingDirectory;

        var process = Process.Start(new ProcessStartInfo(action.ExecutablePath)
        {
            UseShellExecute = true,
            Arguments = action.Arguments ?? string.Empty,
            WorkingDirectory = workingDirectory
        }) ?? throw new InvalidOperationException($"Could not start '{action.Name}'.");

        return Task.FromResult<IExternalProgramHandle>(
            new WindowsExternalProgramHandle(process));
    }
}

internal sealed class WindowsExternalProgramHandle : IExternalProgramHandle
{
    private readonly Process _process;

    public WindowsExternalProgramHandle(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
    }

    public bool IsRunning
    {
        get
        {
            try
            {
                _process.Refresh();
                return !_process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _process.WaitForExitAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Process already ended or was not associated with a handle.
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            _process.Refresh();
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _process.Dispose();
        return ValueTask.CompletedTask;
    }
}
