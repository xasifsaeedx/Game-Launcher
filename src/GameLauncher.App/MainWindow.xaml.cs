using System.Windows;
using System.Windows.Controls;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;
using GameLauncher.Core.Update;
using GameLauncher.Infrastructure.Backup;
using GameLauncher.Infrastructure.Storage;

namespace GameLauncher.App;

public partial class MainWindow : Window
{
    private readonly GameLibraryService _library;
    private readonly LauncherPlayService _play;
    private readonly LaunchProfileService _profiles;
    private readonly GameplayOverlayService _overlay;
    private readonly PersonalLibraryService _personalLibrary;
    private readonly GraphicsOptimizerService _graphics;
    private readonly GameHistoryService _history;
    private readonly GamePreferenceService _preferences;
    private readonly LauncherStatsService _stats;
    private readonly AppPaths _paths;
    private readonly LauncherBackupService _backup;
    private readonly ILauncherUpdateService _updates;
    private readonly CancellationTokenSource _lifetime = new();

    private IReadOnlyList<GameCardViewModel> _cards = Array.Empty<GameCardViewModel>();
    private readonly FilterChoice[] _filters =
    [
        new(LibraryFilterMode.All, "All games"),
        new(LibraryFilterMode.Favorites, "Favorites"),
        new(LibraryFilterMode.NextUp, "Next Up"),
        new(LibraryFilterMode.Playing, "Playing")
    ];

    public MainWindow(
        GameLibraryService library,
        LauncherPlayService play,
        LaunchProfileService profiles,
        GameplayOverlayService overlay,
        PersonalLibraryService personalLibrary,
        GraphicsOptimizerService graphics,
        GameHistoryService history,
        GamePreferenceService preferences,
        LauncherStatsService stats,
        AppPaths paths,
        LauncherBackupService backup,
        ILauncherUpdateService updates)
    {
        InitializeComponent();

        _library = library ?? throw new ArgumentNullException(nameof(library));
        _play = play ?? throw new ArgumentNullException(nameof(play));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
        _personalLibrary = personalLibrary ?? throw new ArgumentNullException(nameof(personalLibrary));
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _backup = backup ?? throw new ArgumentNullException(nameof(backup));
        _updates = updates ?? throw new ArgumentNullException(nameof(updates));

        FilterComboBox.ItemsSource = _filters;
        FilterComboBox.SelectedIndex = 0;

        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _lifetime.Cancel();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await SyncAndRefreshAsync();
    }

    private async void ControllerButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new ControllerModeWindow(
            _library,
            _personalLibrary,
            _preferences,
            _play);

        Hide();
        try
        {
            window.ShowDialog();
        }
        finally
        {
            Show();
            Activate();
            await RefreshLibraryAsync();
        }
    }

    private void StatsButton_Click(object sender, RoutedEventArgs e)
    {
        new StatsWindow(_stats) { Owner = this }.ShowDialog();
    }

    private void ToolsButton_Click(object sender, RoutedEventArgs e)
    {
        new MaintenanceWindow(
            _paths,
            _backup,
            _history,
            _updates)
        {
            Owner = this
        }.ShowDialog();
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

    private async void PersonalLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new PersonalLibraryWindow(_personalLibrary) { Owner = this };
        window.ShowDialog();
        if (window.Changed)
        {
            await RefreshLibraryAsync();
        }
    }

    private async void NextPlayButton_Click(object sender, RoutedEventArgs e)
    {
        new NextPlayWindow(
            _personalLibrary,
            _library,
            _preferences)
        {
            Owner = this
        }.ShowDialog();

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

            await TryAutoPersonalLibrarySyncAsync();
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
            var result = await _play.LaunchAsync(card.Item, _lifetime.Token);
            await RefreshLibraryAsync();

            StatusText.Text =
                $"{card.Title} session saved ({GameCardViewModel.FormatPlaytime(result.Session.DurationSeconds ?? 0)})." +
                (result.GraphicsWarning is null ? string.Empty : $" Graphics: {result.GraphicsWarning}") +
                (result.LibraryWarning is null ? string.Empty : $" Library: {result.LibraryWarning}");
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

    private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid gameId) return;
        var card = _cards.FirstOrDefault(x => x.GameId == gameId);
        if (card is null) return;

        try
        {
            var favorite = await _preferences.ToggleFavoriteAsync(
                card.Item,
                _lifetime.Token);
            await RefreshLibraryAsync();
            StatusText.Text = favorite
                ? $"Added {card.Title} to favorites."
                : $"Removed {card.Title} from favorites.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not update favorite: {ex.Message}";
        }
    }

    private async void RateButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid gameId) return;
        var card = _cards.FirstOrDefault(x => x.GameId == gameId);
        if (card is null) return;

        var dialog = new RateGameWindow(card.Title, card.Rating)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            await _preferences.SetRatingAsync(
                card.Item,
                dialog.Rating,
                _lifetime.Token);
            await RefreshLibraryAsync();
            StatusText.Text = dialog.Rating.HasValue
                ? $"Rated {card.Title} {dialog.Rating}/10."
                : $"Cleared rating for {card.Title}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not update rating: {ex.Message}";
        }
    }

    private void ProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not Guid gameId) return;
        var card = _cards.FirstOrDefault(x => x.GameId == gameId);
        if (card is null) return;

        new LaunchProfileWindow(card.Item, _profiles) { Owner = this }.ShowDialog();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
        ApplyLibraryView();

    private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ApplyLibraryView();

    private void ApplyLibraryView()
    {
        if (GameGrid is null || SearchTextBox is null || FilterComboBox is null) return;

        var filter = FilterComboBox.SelectedItem is FilterChoice choice
            ? choice.Mode
            : LibraryFilterMode.All;
        var search = SearchTextBox.Text;

        var shown = _cards
            .Where(card => LibraryPresentationPolicy.Matches(
                card.Item,
                card.PersonalLibrary,
                card.IsFavorite,
                search,
                filter));

        shown = filter == LibraryFilterMode.NextUp
            ? shown
                .OrderBy(card =>
                    LibraryPresentationPolicy.NextUpSortKey(card.PersonalLibrary))
                .ThenBy(card => card.Title, StringComparer.OrdinalIgnoreCase)
            : shown.OrderBy(card => card.Title, StringComparer.OrdinalIgnoreCase);

        var array = shown.ToArray();
        GameGrid.ItemsSource = array;

        if (_cards.Count > 0)
        {
            StatusText.Text = array.Length == _cards.Count
                ? "Ready."
                : $"Showing {array.Length} of {_cards.Count} installed game(s).";
        }
    }

    private async Task SyncAndRefreshAsync()
    {
        SyncLibraryButton.IsEnabled = false;

        try
        {
            StatusText.Text = "Scanning installed game libraries...";
            var result = await _library.SyncSourcesAsync(_lifetime.Token);
            var libraryWarning = await TryAutoPersonalLibrarySyncAsync();
            var historyWarning = await TryAutoSteamHistorySyncAsync();
            await RefreshLibraryAsync();

            var warningSuffix = result.Warnings.Count == 0
                ? string.Empty
                : $" {result.Warnings.Count} source/artwork warning(s).";

            StatusText.Text =
                $"Library refresh complete: {result.DiscoveredCount} installation(s), " +
                $"{result.MetadataUpdatedCount} cover(s) added.{warningSuffix}" +
                (libraryWarning is null ? string.Empty : $" Personal Library: {libraryWarning}") +
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

    private async Task<string?> TryAutoPersonalLibrarySyncAsync()
    {
        try
        {
            if (!await _personalLibrary.IsAutoSyncEnabledAsync(_lifetime.Token))
            {
                return null;
            }

            await _personalLibrary.SyncAsync(_lifetime.Token);
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
        var personalGames = await _personalLibrary.GetCachedGamesAsync(_lifetime.Token);
        var preferences = await _preferences.GetAllAsync(_lifetime.Token);

        _cards = GameCardViewModelFactory.Create(
            items,
            personalGames,
            preferences);

        ApplyLibraryView();

        var totalSeconds = items.Sum(x => x.TotalPlaytimeSeconds);
        var installs = items.Sum(x => x.Installations.Count);
        var ranked = _cards.Count(x => x.PersonalLibrary is not null);
        var favoriteCount = _cards.Count(x => x.IsFavorite);
        var ratedCount = _cards.Count(x => x.Rating.HasValue);

        LibrarySummaryText.Text =
            $"{items.Count} game(s)  •  {installs} installation(s)  •  " +
            $"{GameCardViewModel.FormatPlaytime(totalSeconds)} tracked  •  " +
            $"{favoriteCount} favorite(s)  •  {ratedCount} rated" +
            (personalGames.Count == 0 ? string.Empty : $"  •  {ranked} matched to Personal Library");
    }

    private sealed record FilterChoice(
        LibraryFilterMode Mode,
        string Label);
}
