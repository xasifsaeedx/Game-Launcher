using GameLauncher.Core.Models;

namespace GameLauncher.Core.Services;

public static class OverlayTextFormatter
{
    public static string Format(OverlaySettings settings, OverlayMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(metrics);

        var parts = new List<string>(3);

        if (settings.ShowFps)
        {
            parts.Add(metrics.FramesPerSecond.HasValue
                ? $"FPS {metrics.FramesPerSecond.Value:0}"
                : "FPS --");
        }

        if (settings.ShowGpuUsage)
        {
            parts.Add(metrics.GpuUsagePercent.HasValue
                ? $"GPU {metrics.GpuUsagePercent.Value:0}%"
                : "GPU --");
        }

        if (settings.ShowGpuTemperature)
        {
            parts.Add(metrics.GpuTemperatureCelsius.HasValue
                ? $"TEMP {metrics.GpuTemperatureCelsius.Value:0}C"
                : "TEMP --");
        }

        return string.Join("  |  ", parts);
    }
}
