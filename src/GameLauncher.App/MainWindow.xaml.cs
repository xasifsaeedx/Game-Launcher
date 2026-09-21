using System.Windows;
using System.Windows.Controls;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class MainWindow : Window
{
    private readonly GameLibraryService _library;
    private readonly GameSessionService _sessions;
    private readonly CancellationTokenSource _lifetime = new();
    private IReadOnlyList<GameCardViewModel> _cards = Array.Empty<GameCardViewModel>();

    public MainWindow(GameLibraryService library, GameSessionService sessions)
    {
        InitializeComponent();
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _lifetime.Cancel();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await SyncAndRefreshAsync();
    }

    private async void SyncSteamButton_Click(object sender, RoutedEventArgs e)
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
            StatusText.Text = $"Launching {card.Title}...";
            var completed = await _sessions.LaunchAndTrackAsync(card.Installation, _lifetime.Token);
            await RefreshLibraryAsync();
            StatusText.Text = $"{card.Title} session saved ({GameCardViewModel.FormatPlaytime(completed.DurationSeconds ?? 0)}).";
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

    private async Task SyncAndRefreshAsync()
    {
        SyncSteamButton.IsEnabled = false;
        try
        {
            StatusText.Text = "Scanning installed Steam games...";
            var result = await _library.SyncSourcesAsync(_lifetime.Token);
            await RefreshLibraryAsync();
            StatusText.Text = result.DiscoveredCount == 0
                ? "Steam scan complete. No installed Steam manifests were found."
                : $"Steam scan complete: {result.DiscoveredCount} installed game(s) found.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Steam scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Steam scan failed: {ex.Message}";
            await RefreshLibraryAsync();
        }
        finally
        {
            SyncSteamButton.IsEnabled = true;
        }
    }

    private async Task RefreshLibraryAsync()
    {
        var items = await _library.GetLibraryAsync(_lifetime.Token);
        _cards = items.Select(x => new GameCardViewModel(x)).ToArray();
        GameGrid.ItemsSource = _cards;

        var totalSeconds = items.Sum(x => x.TotalPlaytimeSeconds);
        LibrarySummaryText.Text = $"{items.Count} game(s)  •  {GameCardViewModel.FormatPlaytime(totalSeconds)} tracked";
    }
}
