namespace GameLauncher.Core.Models;

public sealed record GraphicsOptimizerSettings(
    int TargetWidth,
    int TargetHeight,
    int TargetFps,
    GraphicsQualityPreference QualityPreference,
    GraphicsPerformanceTier? TierOverride,
    bool AutoApplyBeforeLaunch)
{
    public static GraphicsOptimizerSettings Default =>
        new(1920, 1080, 60, GraphicsQualityPreference.Quality, null, false);

    public GraphicsOptimizerSettings Normalize() =>
        this with
        {
            TargetWidth = Math.Clamp(TargetWidth, 640, 7680),
            TargetHeight = Math.Clamp(TargetHeight, 480, 4320),
            TargetFps = Math.Clamp(TargetFps, 30, 360)
        };
}
