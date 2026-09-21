using System.Windows;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class StatsWindow : Window
{
    private readonly LauncherStatsService _stats;

    public StatsWindow(LauncherStatsService stats)
    {
        InitializeComponent();
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        Loaded += StatsWindow_Loaded;
    }

    private async void StatsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= StatsWindow_Loaded;

        try
        {
            var stats = await _stats.GetAsync();

            InstalledText.Text = stats.InstalledGames.ToString();
            FavoritesText.Text = stats.FavoriteGames.ToString();
            LauncherPlaytimeText.Text = GameCardViewModel.FormatPlaytime(stats.LauncherTrackedSeconds);
            AccountPlaytimeText.Text = GameCardViewModel.FormatPlaytime(stats.KnownAccountPlaytimeSeconds);
            MostPlayedText.Text = stats.MostPlayedGame is null
                ? "No tracked sessions yet"
                : $"{stats.MostPlayedGame} · {GameCardViewModel.FormatPlaytime(stats.MostPlayedSeconds)}";
            RecentText.Text = stats.RecentlyPlayedGame is null
                ? "No tracked sessions yet"
                : $"{stats.RecentlyPlayedGame} · {stats.RecentlyPlayedUtc?.ToLocalTime():yyyy-MM-dd}";
            FooterText.Text =
                $"{stats.HistoricalGames} historical game(s) · {stats.NextUpGames} Next Up/Playing · " +
                "playtime totals are intentionally not combined.";
        }
        catch (Exception ex)
        {
            FooterText.Text = $"Could not load stats: {ex.Message}";
        }
    }
}
