using GameLauncher.Core.Models;

namespace GameLauncher.Core.Services;

public sealed class LauncherPlayService
{
    private readonly SmartLaunchService _smartLaunch;
    private readonly GraphicsOptimizerService _graphics;
    private readonly HatchableSyncService _hatchable;

    public LauncherPlayService(
        SmartLaunchService smartLaunch,
        GraphicsOptimizerService graphics,
        HatchableSyncService hatchable)
    {
        _smartLaunch = smartLaunch ?? throw new ArgumentNullException(nameof(smartLaunch));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _hatchable = hatchable ?? throw new ArgumentNullException(nameof(hatchable));
    }

    public async Task<LauncherPlayResult> LaunchAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var graphicsWarning = await TryApplyGraphicsAsync(item, cancellationToken);
        var session = await _smartLaunch.LaunchAsync(
            item,
            cancellationToken: cancellationToken);
        var hatchableWarning = await TryHatchableSyncAsync(cancellationToken);

        return new LauncherPlayResult(
            session,
            graphicsWarning,
            hatchableWarning);
    }

    private async Task<string?> TryApplyGraphicsAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await _graphics.ShouldAutoApplyBeforeLaunchAsync(cancellationToken))
            {
                return null;
            }

            var recommendation = await _graphics.RecommendAsync(item, cancellationToken);
            if (!recommendation.CanApplyAutomatically) return null;

            var result = await _graphics.ApplySafeSettingsAsync(item, cancellationToken);
            return result.Warnings.Count == 0
                ? null
                : string.Join(" ", result.Warnings);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private async Task<string?> TryHatchableSyncAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await _hatchable.IsAutoSyncEnabledAsync(cancellationToken))
            {
                return null;
            }

            await _hatchable.SyncAsync(cancellationToken);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
