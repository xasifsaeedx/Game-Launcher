using System.Windows;
using System.Windows.Controls;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class MainWindow : Window
{
    private readonly GameLibraryService _library;
    private readonly SmartLaunchService _smartLaunch;
    private readonly LaunchProfileService _profiles;
    private readonly GameplayOverlayService _overlay;
    private readonly HatchableSyncService _hatchable;
    private readonly GraphicsOptimizerService _graphics;
    private readonly GameHistoryService _history;
    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyList<GameCardViewModel> _cards = Array.Empty<GameCardViewModel>();

    public MainWindow(
        GameLibraryService library,
        SmartLaunchService smartLaunch,
        LaunchProfileService profiles,
        GameplayOverlayService overlay,
        HatchableSyncService hatchable,
        GraphicsOptimizerService graphics,
        GameHistoryService history)
    {
        InitializeComponent();
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _smartLaunch = smartLaunch ?? throw new ArgumentNullException(nameof(smartLaunch));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _hatchable = hatchable ?? throw new ArgumentNullException(nameof(hatchable));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _lifetime.Cancel();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await SyncAndRefreshAsync();
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        new GamingHistoryWindow(_history) { Owner = this }.ShowDialog();
    }

    private void GraphicsButton_Click(object sender, RoutedEventArgs e)
    {
        new GraphicsOptimizerWindow(_graphics, _library) { Owner = this }.ShowDialog();
    }

    private void OverlayButton_Click(object sender, RoutedEventArgs e)
    {
        new OverlaySettingsWindow(_overlay) { Owner = this }.ShowDialog();
    }

    private async void HatchableButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new HatchableSyncWindow(_hatchable) { Owner = this };
        window.ShowDialog();
        if (window.Changed)
        {
            await RefreshLibraryAsync();
        }
    }

    private async void NextPlayButton_Click(object sender, RoutedEventArgs e)
    {
        new NextPlayWindow(_hatchable, _library) { Owner = this }.ShowDialog();
        await RefreshLibraryAsync();
    }

    private async void SyncLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        await SyncAndRefreshAsync();
    }

    private async void AddGameButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddGameWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            StatusText.Text = $"Adding {dialog.GameTitle}...";
            await _library.AddManualGameAsync(
                dialog.GameTitle,
                dialog.ExecutablePath,
                dialog.CoverImagePath,
                dialog.LaunchArguments,
                _lifetime.Token);

            await TryAutoHatchableSyncAsync();
            await RefreshLibraryAsync();
            StatusText.Text = $"Added {dialog.GameTitle}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not add game: {ex.Message}";
        }
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid gameId) return;
        var card = _cards.FirstOrDefault(x => x.GameId == gameId);
        if (card?.Installation is null)
        {
            StatusText.Text = "No installed copy is available for this game.";
            return;
        }

        button.IsEnabled = false;
        try
        {
            var graphicsWarning = await TryAutoGraphicsApplyAsync(card.Item);
            StatusText.Text = $"Launching {card.Title}...";

            var completed = await _smartLaunch.LaunchAsync(
                card.Item,
                cancellationToken: _lifetime.Token);

            var syncWarning = await TryAutoHatchableSyncAsync();
            await RefreshLibraryAsync();

            StatusText.Text =
                $"{card.Title} session saved ({GameCardViewModel.FormatPlaytime(completed.DurationSeconds ?? 0)})." +
                (graphicsWarning is null ? string.Empty : $" Graphics: {graphicsWarning}") +
                (syncWarning is null ? string.Empty : $" Hatchable: {syncWarning}");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Session tracking stopped.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not launch {card.Title}: {ex.Message}";
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void ProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid gameId) return;
        var card = _cards.FirstOrDefault(x => x.GameId == gameId);
        if (card is null) return;

        new LaunchProfileWindow(card.Item, _profiles) { Owner = this }.ShowDialog();
    }

    private async Task<string?> TryAutoGraphicsApplyAsync(GameLibraryItem item)
    {
        try
        {
            if (!await _graphics.ShouldAutoApplyBeforeLaunchAsync(_lifetime.Token))
            {
                return null;
            }

            var recommendation = await _graphics.RecommendAsync(item, _lifetime.Token);
            if (!recommendation.CanApplyAutomatically)
            {
                return null;
            }

            var result = await _graphics.ApplySafeSettingsAsync(item, _lifetime.Token);
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

    private async Task SyncAndRefreshAsync()
    {
        SyncLibraryButton.IsEnabled = false;
        try
        {
            StatusText.Text = "Scanning installed game libraries...";
            var result = await _library.SyncSourcesAsync(_lifetime.Token);
            var hatchableWarning = await TryAutoHatchableSyncAsync();
            var historyWarning = await TryAutoSteamHistorySyncAsync();
            await RefreshLibraryAsync();

            var warningSuffix = result.Warnings.Count == 0
                ? string.Empty
                : $" {result.Warnings.Count} source/artwork warning(s).";

            StatusText.Text =
                $"Library sync complete: {result.DiscoveredCount} installation(s) discovered, " +
                $"{result.MetadataUpdatedCount} cover(s) added.{warningSuffix}" +
                (hatchableWarning is null ? string.Empty : $" Hatchable: {hatchableWarning}") +
                (historyWarning is null ? string.Empty : $" Steam history: {historyWarning}");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Library scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Library scan failed: {ex.Message}";
            await RefreshLibraryAsync();
        }
        finally
        {
            SyncLibraryButton.IsEnabled = true;
        }
    }

    private async Task<string?> TryAutoSteamHistorySyncAsync()
    {
        try
        {
            if (!await _history.IsSteamAutoSyncEnabledAsync(_lifetime.Token))
            {
                return null;
            }

            await _history.SyncSteamAsync(_lifetime.Token);
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

    private async Task<string?> TryAutoHatchableSyncAsync()
    {
        try
        {
            if (!await _hatchable.IsAutoSyncEnabledAsync(_lifetime.Token))
            {
                return null;
            }

            await _hatchable.SyncAsync(_lifetime.Token);
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

    private async Task RefreshLibraryAsync()
    {
        var items = await _library.GetLibraryAsync(_lifetime.Token);
        var remote = await _hatchable.GetCachedGamesAsync(_lifetime.Token);

        _cards = items
            .Select(item => new GameCardViewModel(
                item,
                HatchableSyncService.FindMatch(item, remote)))
            .ToArray();

        GameGrid.ItemsSource = _cards;

        var totalSeconds = items.Sum(x => x.TotalPlaytimeSeconds);
        var installs = items.Sum(x => x.Installations.Count);
        var ranked = _cards.Count(x => x.Hatchable is not null);

        LibrarySummaryText.Text =
            $"{items.Count} game(s)  •  {installs} installation(s)  •  " +
            $"{GameCardViewModel.FormatPlaytime(totalSeconds)} tracked" +
            (remote.Count == 0 ? string.Empty : $"  •  {ranked} matched to Next 100");
    }
}
