using GameLauncher.Core.Models;

namespace GameLauncher.Core.Services;

public sealed class LauncherPlayService
{
    private readonly SmartLaunchService _smartLaunch;
    private readonly GraphicsOptimizerService _graphics;
    private readonly PersonalLibraryService _personalLibrary;

    public LauncherPlayService(
        SmartLaunchService smartLaunch,
        GraphicsOptimizerService graphics,
        PersonalLibraryService personalLibrary)
    {
        _smartLaunch = smartLaunch ?? throw new ArgumentNullException(nameof(smartLaunch));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _personalLibrary = personalLibrary ?? throw new ArgumentNullException(nameof(personalLibrary));
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
        var libraryWarning = await TryPersonalLibraryRefreshAsync(cancellationToken);

        return new LauncherPlayResult(
            session,
            graphicsWarning,
            libraryWarning);
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

    private async Task<string?> TryPersonalLibraryRefreshAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await _personalLibrary.IsAutoSyncEnabledAsync(cancellationToken))
            {
                return null;
            }

            await _personalLibrary.SyncAsync(cancellationToken);
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
