using GameLauncher.Core.Models;

namespace GameLauncher.App;

public sealed record GraphicsGameViewModel(
    GameLibraryItem Item,
    GraphicsRecommendation Recommendation)
{
    public Guid GameId => Item.Game.Id;
    public string Title => Item.Game.Title;
    public string Preset => Recommendation.RecommendedPreset;
    public string Support => Recommendation.Profile is null
        ? "Generic"
        : Recommendation.CanApplyAutomatically
            ? "Safe apply"
            : "Recommendations";
    public string SafeChanges => Recommendation.SafePatches.Count == 0
        ? "—"
        : Recommendation.SafePatches.Count.ToString();
}
