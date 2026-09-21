namespace GameLauncher.Core.Models;

public sealed record OverlaySettings(
    bool Enabled,
    bool ShowFps,
    bool ShowGpuUsage,
    bool ShowGpuTemperature,
    int UpdateIntervalMs)
{
    public static OverlaySettings Default { get; } =
        new(true, true, true, true, 500);

    public OverlaySettings Normalize() =>
        this with { UpdateIntervalMs = Math.Clamp(UpdateIntervalMs, 250, 5000) };
}
