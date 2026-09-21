namespace GameLauncher.Core.Models;

public sealed record HardwareProfile(
    string CpuName,
    string GpuName,
    double RamGb,
    int DisplayWidth,
    int DisplayHeight,
    int RefreshRateHz,
    GraphicsPerformanceTier DetectedTier,
    DateTimeOffset DetectedUtc)
{
    public string DisplaySummary =>
        DisplayWidth > 0 && DisplayHeight > 0
            ? $"{DisplayWidth}x{DisplayHeight} @ {Math.Max(1, RefreshRateHz)} Hz"
            : "Display unknown";

    public string RamSummary => RamGb > 0 ? $"{RamGb:0.#} GB RAM" : "RAM unknown";
}
