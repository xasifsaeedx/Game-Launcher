namespace GameLauncher.Core.Models;

public sealed record SafeGraphicsPatch(
    string DisplayName,
    string Key,
    string ValueTemplate);

public sealed record GameGraphicsProfile(
    string Id,
    string Title,
    long? SteamAppId,
    IReadOnlyList<string> AlternateTitles,
    string? ConfigPathTemplate,
    GraphicsConfigFormat? ConfigFormat,
    IReadOnlyList<SafeGraphicsPatch> SafePatches,
    IReadOnlyList<string> GameSpecificTips)
{
    public bool SupportsSafeApply =>
        !string.IsNullOrWhiteSpace(ConfigPathTemplate) &&
        ConfigFormat.HasValue &&
        SafePatches.Count > 0;
}
