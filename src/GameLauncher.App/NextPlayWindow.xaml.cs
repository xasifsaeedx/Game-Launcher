using System.Windows;
using System.Windows.Controls;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class NextPlayWindow : Window
{
    private readonly HatchableSyncService _sync;
    private readonly GameLibraryService _library;
    private readonly Choice<string?>[] _progressChoices;
    private readonly Choice<int?>[] _ratingChoices;
    private IReadOnlyList<NextPlayGameViewModel> _rows = Array.Empty<NextPlayGameViewModel>();

    public NextPlayWindow(
        HatchableSyncService sync,
        GameLibraryService library)
    {
        InitializeComponent();
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _library = library ?? throw new ArgumentNullException(nameof(library));

        _progressChoices =
        [
            new(null, "Not started"),
            new("playing", "Playing"),
            new("completed", "Completed"),
            new("paused", "Paused"),
            new("dropped", "Dropped")
        ];
        ProgressComboBox.ItemsSource = _progressChoices;

        var ratings = new List<Choice<int?>> { new(null, "Not rated") };
        ratings.AddRange(
            Enumerable.Range(1, 10)
                .Reverse()
                .Select(x => new Choice<int?>(x, $"{x} / 10")));
        _ratingChoices = ratings.ToArray();
        RatingComboBox.ItemsSource = _ratingChoices;

        Loaded += NextPlayWindow_Loaded;
    }

    private async void NextPlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= NextPlayWindow_Loaded;
        await ReloadAsync(syncFirst: false);
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        await ReloadAsync(syncFirst: true);
    }

    private void GamesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GamesGrid.SelectedItem is not NextPlayGameViewModel selected)
        {
            SelectedTitleText.Text = string.Empty;
            return;
        }

        SelectedTitleText.Text = $"#{selected.Rank}  {selected.Title}";
        ProgressComboBox.SelectedItem =
            _progressChoices.First(x =>
                string.Equals(
                    x.Value,
                    selected.Remote.ProgressStatus,
                    StringComparison.OrdinalIgnoreCase));

        RatingComboBox.SelectedItem =
            _ratingChoices.First(x => x.Value == selected.Remote.Rating);

        SelectionStatusText.Text =
            $"{selected.LibraryStatus} · {selected.Playtime}" +
            (selected.IsInstalled ? " · Installed locally" : string.Empty);
    }

    private async void SaveProgress_Click(object sender, RoutedEventArgs e)
    {
        if (GamesGrid.SelectedItem is not NextPlayGameViewModel selected) return;

        try
        {
            SelectionStatusText.Text = "Saving...";

            var progress = ProgressComboBox.SelectedItem is Choice<string?> progressChoice
                ? progressChoice.Value
                : null;
            var rating = RatingComboBox.SelectedItem is Choice<int?> ratingChoice
                ? ratingChoice.Value
                : null;

            await _sync.UpdateRemoteStateAsync(
                selected.RemoteGameId,
                progress,
                rating);

            await ReloadAsync(syncFirst: false, selected.RemoteGameId);
            SelectionStatusText.Text = "Saved to Hatchable.";
        }
        catch (Exception ex)
        {
            SelectionStatusText.Text = ex.Message;
        }
    }

    private async Task ReloadAsync(
        bool syncFirst,
        int? selectRemoteGameId = null)
    {
        try
        {
            if (syncFirst)
            {
                StatusText.Text = "Syncing with Hatchable...";
                await _sync.SyncAsync();
            }

            var remote = await _sync.GetCachedGamesAsync();
            if (remote.Count == 0)
            {
                StatusText.Text = "No cached ranking yet. Connect Hatchable Sync and run Sync now.";
                GamesGrid.ItemsSource = Array.Empty<NextPlayGameViewModel>();
                return;
            }

            var local = await _library.GetLibraryAsync();
            _rows = remote
                .OrderBy(x => x.RankScore)
                .Select(r => new NextPlayGameViewModel(
                    r,
                    local.Any(l => HatchableSyncService.FindMatch(
                        l,
                        new[] { r }) is not null)))
                .ToArray();

            GamesGrid.ItemsSource = _rows;
            GamesGrid.SelectedItem = selectRemoteGameId.HasValue
                ? _rows.FirstOrDefault(x => x.RemoteGameId == selectRemoteGameId.Value)
                : _rows.FirstOrDefault();

            StatusText.Text =
                $"{_rows.Count} ranked game(s) · {_rows.Count(x => x.IsInstalled)} installed locally";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private sealed record Choice<T>(T Value, string Label);
}
