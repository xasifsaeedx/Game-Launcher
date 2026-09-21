namespace GameLauncher.Core.Models;

public sealed record OverlayMetrics(
    double? FramesPerSecond,
    double? GpuUsagePercent,
    double? GpuTemperatureCelsius);
