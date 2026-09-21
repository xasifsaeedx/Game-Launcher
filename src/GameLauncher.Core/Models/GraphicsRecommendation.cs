namespace GameLauncher.Core.Models;

public sealed record GraphicsRecommendation(
    string GameTitle,
    string RecommendedPreset,
    GraphicsPerformanceTier EffectiveTier,
    GraphicsOptimizerSettings Target,
    GameGraphicsProfile? Profile,
    string? ConfigPath,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<SafeGraphicsPatch> SafePatches)
{
    public bool CanApplyAutomatically =>
        Profile?.SupportsSafeApply == true &&
        !string.IsNullOrWhiteSpace(ConfigPath) &&
        SafePatches.Count > 0;
}
