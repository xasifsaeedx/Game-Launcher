using GameLauncher.Core.Models;

namespace GameLauncher.Core.Services;

public static class GraphicsRecommendationPolicy
{
    public static string RecommendPreset(
        GraphicsPerformanceTier tier,
        GraphicsQualityPreference preference) =>
        tier switch
        {
            GraphicsPerformanceTier.Entry1080p => preference switch
            {
                GraphicsQualityPreference.Performance => "Low",
                GraphicsQualityPreference.Balanced => "Medium",
                _ => "Medium"
            },
            GraphicsPerformanceTier.Mainstream1080p => preference switch
            {
                GraphicsQualityPreference.Performance => "Medium",
                GraphicsQualityPreference.Balanced => "High",
                _ => "High"
            },
            GraphicsPerformanceTier.Strong1080p => preference switch
            {
                GraphicsQualityPreference.Performance => "High",
                GraphicsQualityPreference.Balanced => "High",
                _ => "Ultra"
            },
            GraphicsPerformanceTier.HighEnd => preference switch
            {
                GraphicsQualityPreference.Performance => "High",
                _ => "Ultra"
            },
            GraphicsPerformanceTier.Enthusiast => "Ultra",
            _ => preference == GraphicsQualityPreference.Performance
                ? "Medium"
                : "High"
        };

    public static IReadOnlyList<string> BuildGeneralTips(
        GraphicsPerformanceTier tier,
        GraphicsOptimizerSettings target)
    {
        var tips = new List<string>
        {
            $"Resolution: {target.TargetWidth}x{target.TargetHeight}.",
            $"Frame-rate target: {target.TargetFps} FPS.",
            $"Start from the {RecommendPreset(tier, target.QualityPreference)} preset."
        };

        if (tier <= GraphicsPerformanceTier.Mainstream1080p || tier == GraphicsPerformanceTier.Unknown)
        {
            tips.Add("Ray tracing: Off unless a game-specific profile says otherwise.");
            tips.Add("MSAA / supersampling: Off; prefer temporal AA or a quality upscaler when needed.");
            tips.Add("Textures: High first; reduce only if VRAM pressure or stutter appears.");
            tips.Add("Shadows / volumetrics / crowd density: reduce these before texture quality.");
        }
        else if (tier == GraphicsPerformanceTier.Strong1080p)
        {
            tips.Add("Ray tracing: Off by default; enable selectively only with comfortable FPS headroom.");
            tips.Add("Textures: High/Ultra; keep expensive shadows and volumetrics one step below maximum if needed.");
        }
        else
        {
            tips.Add("Start at Ultra, then reduce ray tracing, shadows, and volumetrics first if the FPS target is missed.");
        }

        return tips;
    }
}
