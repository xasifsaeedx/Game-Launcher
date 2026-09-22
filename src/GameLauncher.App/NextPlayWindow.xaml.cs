using System.Windows;
using System.Windows.Controls;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class NextPlayWindow : Window
{
    private readonly PersonalLibraryService _personalLibrary;
    private readonly GameLibraryService _library;
    private readonly GamePreferenceService _preferences;
    private readonly Choice<int?>[] _ratingChoices;
    private IReadOnlyList<NextPlayGameViewModel> _rows = Array.Empty<NextPlayGameViewModel>();

    public NextPlayWindow(
        PersonalLibraryService personalLibrary,
        GameLibraryService library,
        GamePreferenceService preferences)
    {
        InitializeComponent();
        _personalLibrary = personalLibrary ?? throw new ArgumentNullException(nameof(personalLibrary));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));

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
            RatingComboBox.SelectedIndex = 0;
            SelectionStatusText.Text = string.Empty;
            return;
        }

        SelectedTitleText.Text = selected.Rank.HasValue
            ? $"#{selected.Rank}  {selected.Title}"
            : selected.Title;

        RatingComboBox.SelectedItem =
            _ratingChoices.First(x => x.Value == selected.EffectiveRating);

        SelectionStatusText.Text =
            $"{selected.LibraryStatus} · {selected.Progress}" +
            (selected.IsInstalled ? " · Installed locally" : string.Empty);
    }

    private async void SaveRating_Click(object sender, RoutedEventArgs e)
    {
        if (GamesGrid.SelectedItem is not NextPlayGameViewModel selected) return;

        try
        {
            var rating = RatingComboBox.SelectedItem is Choice<int?> choice
                ? choice.Value
                : null;

            await _preferences.SetRatingAsync(selected.Title, rating);
            await ReloadAsync(syncFirst: false, selected.SourceRow);
            SelectionStatusText.Text = rating.HasValue
                ? $"Saved {rating}/10 locally."
                : "Local rating cleared.";
        }
        catch (Exception ex)
        {
            SelectionStatusText.Text = ex.Message;
        }
    }

    private async Task ReloadAsync(
        bool syncFirst,
        int? selectSourceRow = null)
    {
        try
        {
            if (syncFirst)
            {
                StatusText.Text = "Refreshing Google Sheets...";
                await _personalLibrary.SyncAsync();
            }

            var personalGames = await _personalLibrary.GetCachedGamesAsync();
            if (personalGames.Count == 0)
            {
                StatusText.Text = "No personal-library data yet. Link a Google Sheet from Personal Library and refresh it.";
                GamesGrid.ItemsSource = Array.Empty<NextPlayGameViewModel>();
                return;
            }

            var local = await _library.GetLibraryAsync();
            var preferences = await _preferences.GetAllAsync();

            _rows = personalGames
                .OrderBy(x => x.Rank ?? int.MaxValue)
                .ThenBy(x => x.SourceRow)
                .Select(r =>
                {
                    preferences.TryGetValue(
                        GamePreferenceService.GetGameKey(r.Title),
                        out var preference);

                    return new NextPlayGameViewModel(
                        r,
                        local.Any(l => PersonalLibraryMatcher.FindMatch(l, new[] { r }) is not null),
                        preference?.Rating);
                })
                .ToArray();

            GamesGrid.ItemsSource = _rows;
            GamesGrid.SelectedItem = selectSourceRow.HasValue
                ? _rows.FirstOrDefault(x => x.SourceRow == selectSourceRow.Value)
                : _rows.FirstOrDefault();

            StatusText.Text =
                $"{_rows.Count} game(s) from Google Sheets · {_rows.Count(x => x.IsInstalled)} installed locally";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private sealed record Choice<T>(T Value, string Label);
}
