using System.Diagnostics;
using GameLauncher.Core.Models;
using GameLauncher.Core.Runtime;
using GameLauncher.Core.Services;

namespace GameLauncher.Infrastructure.Overlay;

public sealed class RtssGameplayOverlayRuntime : IGameplayOverlayRuntime
{
    public async Task<IOverlaySession> StartAsync(
        int processId,
        OverlaySettings settings,
        CancellationToken cancellationToken = default)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        ArgumentNullException.ThrowIfNull(settings);

        await EnsureRtssAvailableAsync(cancellationToken);

        var sharedMemory = RtssSharedMemoryClient.Open();
        GpuMetricsReader? gpu = null;

        try
        {
            if (settings.ShowGpuUsage || settings.ShowGpuTemperature)
            {
                gpu = new GpuMetricsReader();
            }

            return new RtssOverlaySession(
                sharedMemory,
                gpu,
                processId,
                settings.Normalize());
        }
        catch
        {
            gpu?.Dispose();
            sharedMemory.Dispose();
            throw;
        }
    }

    public Task<OverlayRuntimeStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (RtssSharedMemoryClient.IsAvailable())
        {
            return Task.FromResult(
                new OverlayRuntimeStatus(
                    true,
                    "RTSS is running and ready.",
                    RtssLocator.FindExecutable()));
        }

        var path = RtssLocator.FindExecutable();
        return Task.FromResult(
            path is null
                ? new OverlayRuntimeStatus(
                    false,
                    "RTSS was not found. Install RivaTuner Statistics Server to enable the in-game overlay.")
                : new OverlayRuntimeStatus(
                    true,
                    "RTSS is installed and will start automatically with a game.",
                    path));
    }

    private static async Task EnsureRtssAvailableAsync(
        CancellationToken cancellationToken)
    {
        if (RtssSharedMemoryClient.IsAvailable()) return;

        var executable = RtssLocator.FindExecutable()
            ?? throw new InvalidOperationException(
                "RivaTuner Statistics Server is not installed.");

        Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        });

        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (RtssSharedMemoryClient.IsAvailable()) return;
            await Task.Delay(250, cancellationToken);
        }

        throw new InvalidOperationException(
            "RTSS was started but its shared memory did not become available.");
    }

    private sealed class RtssOverlaySession : IOverlaySession
    {
        private readonly RtssSharedMemoryClient _sharedMemory;
        private readonly GpuMetricsReader? _gpu;
        private readonly int _processId;
        private readonly OverlaySettings _settings;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly Task _loop;
        private bool _disposed;

        public RtssOverlaySession(
            RtssSharedMemoryClient sharedMemory,
            GpuMetricsReader? gpu,
            int processId,
            OverlaySettings settings)
        {
            _sharedMemory = sharedMemory;
            _gpu = gpu;
            _processId = processId;
            _settings = settings;
            _loop = Task.Run(UpdateLoopAsync);
        }

        private async Task UpdateLoopAsync()
        {
            while (!_lifetime.IsCancellationRequested)
            {
                try
                {
                    var fps = _settings.ShowFps
                        ? _sharedMemory.ReadFramesPerSecond(_processId)
                        : null;

                    var gpuMetrics = (_settings.ShowGpuUsage || _settings.ShowGpuTemperature) &&
                                     _gpu is not null
                        ? _gpu.Read()
                        : (UsagePercent: (double?)null, TemperatureCelsius: (double?)null);

                    var metrics = new OverlayMetrics(
                        fps,
                        _settings.ShowGpuUsage ? gpuMetrics.UsagePercent : null,
                        _settings.ShowGpuTemperature ? gpuMetrics.TemperatureCelsius : null);

                    _sharedMemory.WriteOverlay(
                        OverlayTextFormatter.Format(_settings, metrics));
                }
                catch
                {
                    // Keep the gameplay loop alive through transient telemetry/RTSS errors.
                }

                try
                {
                    await Task.Delay(
                        _settings.UpdateIntervalMs,
                        _lifetime.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            _lifetime.Cancel();
            try
            {
                await _loop;
            }
            catch
            {
                // Cleanup continues even if the telemetry loop faulted.
            }

            _sharedMemory.ClearOverlay();
            _gpu?.Dispose();
            _sharedMemory.Dispose();
            _lifetime.Dispose();
        }
    }
}
