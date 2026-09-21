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
            ? RefreshRateHz > 1
                ? $"{DisplayWidth}x{DisplayHeight} @ {RefreshRateHz} Hz"
                : $"{DisplayWidth}x{DisplayHeight}"
            : "Display unknown";

    public string RamSummary => RamGb > 0 ? $"{RamGb:0.#} GB RAM" : "RAM unknown";
}
