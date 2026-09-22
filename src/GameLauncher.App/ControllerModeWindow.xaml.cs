using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;
using GameLauncher.Infrastructure.Runtime;

namespace GameLauncher.App;

public partial class ControllerModeWindow : Window
{
    private readonly GameLibraryService _library;
    private readonly PersonalLibraryService _personalLibrary;
    private readonly GamePreferenceService _preferences;
    private readonly LauncherPlayService _play;
    private readonly XInputGamepad _gamepad = new();
    private readonly DispatcherTimer _controllerTimer;
    private readonly CancellationTokenSource _lifetime = new();

    private IReadOnlyList<GameCardViewModel> _cards = Array.Empty<GameCardViewModel>();
    private LibraryFilterMode _filter = LibraryFilterMode.All;
    private bool _busy;

    public ControllerModeWindow(
        GameLibraryService library,
        PersonalLibraryService personalLibrary,
        GamePreferenceService preferences,
        LauncherPlayService play)
    {
        InitializeComponent();

        _library = library ?? throw new ArgumentNullException(nameof(library));
        _personalLibrary = personalLibrary ?? throw new ArgumentNullException(nameof(personalLibrary));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _play = play ?? throw new ArgumentNullException(nameof(play));

        _controllerTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(75)
        };
        _controllerTimer.Tick += ControllerTimer_Tick;

        Loaded += ControllerModeWindow_Loaded;
        Closed += ControllerModeWindow_Closed;
    }

    private async void ControllerModeWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ControllerModeWindow_Loaded;
        await RefreshAsync();
        _controllerTimer.Start();
        Focus();
    }

    private void ControllerModeWindow_Closed(object? sender, EventArgs e)
    {
        _controllerTimer.Stop();
        _lifetime.Cancel();
    }

    private async void ControllerTimer_Tick(object? sender, EventArgs e)
    {
        if (_busy) return;

        var pressed = _gamepad.PollPressedButtons();
        if (pressed == GamepadButtons.None) return;

        if (pressed.HasFlag(GamepadButtons.B))
        {
            Close();
            return;
        }

        if (pressed.HasFlag(GamepadButtons.X))
        {
            CycleFilter();
            await RefreshAsync();
            return;
        }

        if (pressed.HasFlag(GamepadButtons.Y))
        {
            await ToggleSelectedFavoriteAsync();
            return;
        }

        if (pressed.HasFlag(GamepadButtons.A))
        {
            await LaunchSelectedAsync();
            return;
        }

        if (pressed.HasFlag(GamepadButtons.DPadLeft))
            MoveSelection(-1);
        else if (pressed.HasFlag(GamepadButtons.DPadRight))
            MoveSelection(1);
        else if (pressed.HasFlag(GamepadButtons.DPadUp))
            MoveSelection(-ColumnCount());
        else if (pressed.HasFlag(GamepadButtons.DPadDown))
            MoveSelection(ColumnCount());
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_busy) return;

        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Enter:
                await LaunchSelectedAsync();
                e.Handled = true;
                break;
            case Key.F:
                await ToggleSelectedFavoriteAsync();
                e.Handled = true;
                break;
            case Key.Tab:
                CycleFilter();
                await RefreshAsync();
                e.Handled = true;
                break;
            case Key.Left:
                MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveSelection(-ColumnCount());
                e.Handled = true;
                break;
            case Key.Down:
                MoveSelection(ColumnCount());
                e.Handled = true;
                break;
        }
    }

    private async Task RefreshAsync(Guid? preserveGameId = null)
    {
        var library = await _library.GetLibraryAsync(_lifetime.Token);
        var remote = await _personalLibrary.GetCachedGamesAsync(_lifetime.Token);
        var preferences = await _preferences.GetAllAsync(_lifetime.Token);

        _cards = library
            .Select(item =>
            {
                var personal = PersonalLibraryService.FindMatch(item, remote);
                preferences.TryGetValue(
                    GamePreferenceService.GetGameKey(item.Game.Title),
                    out var preference);
                return new GameCardViewModel(item, personal, preference);
            })
            .Where(card => LibraryPresentationPolicy.Matches(
                card.Item,
                card.PersonalLibrary,
                card.IsFavorite,
                null,
                _filter))
            .OrderBy(card => _filter == LibraryFilterMode.NextUp
                ? LibraryPresentationPolicy.NextUpSortKey(card.PersonalLibrary)
                : 0)
            .ThenBy(card => card.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        GameList.ItemsSource = _cards;

        var selectedIndex = preserveGameId.HasValue
            ? Array.FindIndex(_cards.ToArray(), x => x.GameId == preserveGameId.Value)
            : 0;

        if (_cards.Count > 0)
        {
            GameList.SelectedIndex = Math.Max(0, selectedIndex);
            GameList.ScrollIntoView(GameList.SelectedItem);
        }

        FilterText.Text = _filter switch
        {
            LibraryFilterMode.Favorites => "FAVORITES",
            LibraryFilterMode.NextUp => "NEXT UP",
            LibraryFilterMode.Playing => "PLAYING",
            _ => "ALL GAMES"
        };
        SummaryText.Text =
            $"{_cards.Count} game(s) · " +
            (_gamepad.IsConnected ? "Xbox controller connected" : "Controller/keyboard ready");
    }

    private void GameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedText.Text = GameList.SelectedItem is GameCardViewModel selected
            ? selected.Title
            : string.Empty;
    }

    private void MoveSelection(int delta)
    {
        if (_cards.Count == 0) return;

        var index = GameList.SelectedIndex < 0 ? 0 : GameList.SelectedIndex;
        index = Math.Clamp(index + delta, 0, _cards.Count - 1);
        GameList.SelectedIndex = index;
        GameList.ScrollIntoView(GameList.SelectedItem);
    }

    private int ColumnCount() =>
        Math.Max(1, (int)Math.Floor(Math.Max(220, GameList.ActualWidth) / 216d));

    private void CycleFilter()
    {
        _filter = _filter switch
        {
            LibraryFilterMode.All => LibraryFilterMode.Favorites,
            LibraryFilterMode.Favorites => LibraryFilterMode.NextUp,
            LibraryFilterMode.NextUp => LibraryFilterMode.Playing,
            _ => LibraryFilterMode.All
        };
    }

    private async Task ToggleSelectedFavoriteAsync()
    {
        if (GameList.SelectedItem is not GameCardViewModel selected) return;

        await _preferences.ToggleFavoriteAsync(selected.Item, _lifetime.Token);
        await RefreshAsync(selected.GameId);
        StatusText.Text = selected.IsFavorite
            ? $"Removed {selected.Title} from favorites."
            : $"Added {selected.Title} to favorites.";
    }

    private async Task LaunchSelectedAsync()
    {
        if (GameList.SelectedItem is not GameCardViewModel selected ||
            selected.Installation is null)
        {
            return;
        }

        _busy = true;
        StatusText.Text = $"Launching {selected.Title}...";

        try
        {
            WindowState = WindowState.Minimized;

            var result = await _play.LaunchAsync(selected.Item, _lifetime.Token);
            await RefreshAsync(selected.GameId);

            StatusText.Text =
                $"{selected.Title} · {GameCardViewModel.FormatPlaytime(result.Session.DurationSeconds ?? 0)}" +
                (result.GraphicsWarning is null ? string.Empty : $" · Graphics: {result.GraphicsWarning}") +
                (result.LibraryWarning is null ? string.Empty : $" · Library: {result.LibraryWarning}");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Launch tracking stopped.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not launch {selected.Title}: {ex.Message}";
        }
        finally
        {
            WindowState = WindowState.Maximized;
            Activate();
            Focus();
            _busy = false;
        }
    }
}
