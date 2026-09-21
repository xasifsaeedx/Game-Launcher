using GameLauncher.Core.Graphics;
using GameLauncher.Core.Hardware;
using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;

namespace GameLauncher.Core.Services;

public sealed class GraphicsOptimizerService
{
    private readonly IGraphicsOptimizerRepository _repository;
    private readonly IHardwareProfileDetector _hardware;
    private readonly GraphicsProfileCatalog _catalog;
    private readonly IGraphicsConfigRuntime _runtime;

    public GraphicsOptimizerService(
        IGraphicsOptimizerRepository repository,
        IHardwareProfileDetector hardware,
        GraphicsProfileCatalog catalog,
        IGraphicsConfigRuntime runtime)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public Task<GraphicsOptimizerSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetSettingsAsync(cancellationToken);

    public Task SaveSettingsAsync(
        GraphicsOptimizerSettings settings,
        CancellationToken cancellationToken = default) =>
        _repository.SaveSettingsAsync(settings.Normalize(), cancellationToken);

    public Task<HardwareProfile> DetectHardwareAsync(
        CancellationToken cancellationToken = default) =>
        _hardware.DetectAsync(cancellationToken);

    public async Task<GraphicsRecommendation> RecommendAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        var settings = (await _repository.GetSettingsAsync(cancellationToken)).Normalize();
        var hardware = await _hardware.DetectAsync(cancellationToken);
        return Recommend(item, settings, hardware);
    }

    public GraphicsRecommendation Recommend(
        GameLibraryItem item,
        GraphicsOptimizerSettings settings,
        HardwareProfile hardware)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(hardware);

        settings = settings.Normalize();
        var tier = settings.TierOverride ?? hardware.DetectedTier;
        var profile = _catalog.FindMatch(item);
        var tips = GraphicsRecommendationPolicy
            .BuildGeneralTips(tier, settings)
            .ToList();

        if (profile is not null)
        {
            tips.AddRange(profile.GameSpecificTips);
        }
        else
        {
            tips.Add("No verified automatic config profile exists for this game; use these as in-game recommendations only.");
        }

        var configPath = profile?.ConfigPathTemplate is null
            ? null
            : ExpandPath(profile.ConfigPathTemplate);

        return new GraphicsRecommendation(
            item.Game.Title,
            GraphicsRecommendationPolicy.RecommendPreset(tier, settings.QualityPreference),
            tier,
            settings,
            profile,
            configPath,
            tips,
            profile?.SafePatches ?? Array.Empty<SafeGraphicsPatch>());
    }

    public async Task<GraphicsApplyResult> ApplySafeSettingsAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        var settings = (await _repository.GetSettingsAsync(cancellationToken)).Normalize();
        var hardware = await _hardware.DetectAsync(cancellationToken);
        var recommendation = Recommend(item, settings, hardware);
        return await _runtime.ApplyAsync(recommendation, hardware, cancellationToken);
    }

    public async Task<GraphicsApplyResult> RestoreOriginalAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await RecommendAsync(item, cancellationToken);
        return await _runtime.RestoreAsync(recommendation, cancellationToken);
    }

    public async Task<bool> ShouldAutoApplyBeforeLaunchAsync(
        CancellationToken cancellationToken = default) =>
        (await _repository.GetSettingsAsync(cancellationToken)).AutoApplyBeforeLaunch;

    private static string ExpandPath(string path)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var expanded = Environment
            .ExpandEnvironmentVariables(path)
            .Replace("{documents}", documents, StringComparison.OrdinalIgnoreCase);

        if (expanded.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            expanded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                expanded[2..]);
        }

        return Path.GetFullPath(expanded);
    }
}
