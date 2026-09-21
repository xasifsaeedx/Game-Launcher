using System.Windows;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;
using Microsoft.Win32;

namespace GameLauncher.App;

public partial class GamingHistoryWindow : Window
{
    private readonly GameHistoryService _history;

    public GamingHistoryWindow(GameHistoryService history)
    {
        InitializeComponent();
        _history = history ?? throw new ArgumentNullException(nameof(history));

        ImportSourceComboBox.ItemsSource = new[]
        {
            GameSource.PlayStation,
            GameSource.Xbox,
            GameSource.MicrosoftStore,
            GameSource.Gog,
            GameSource.EA,
            GameSource.Ubisoft,
            GameSource.BattleNet,
            GameSource.Epic,
            GameSource.Steam
        };
        ImportSourceComboBox.SelectedItem = GameSource.PlayStation;

        Loaded += GamingHistoryWindow_Loaded;
    }

    private async void GamingHistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= GamingHistoryWindow_Loaded;
        await RefreshAsync();
    }

    private async void SteamSetup_Click(object sender, RoutedEventArgs e)
    {
        var window = new SteamHistorySettingsWindow(_history) { Owner = this };
        window.ShowDialog();
        if (window.Changed) await RefreshAsync();
    }

    private async void SyncSteam_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Syncing Steam owned games and playtime...";
            var result = await _history.SyncSteamAsync();
            StatusText.Text =
                $"Steam sync complete: {result.StoredCount}/{result.DiscoveredCount} game(s) stored.";
            await RefreshAsync(keepStatus: true);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        if (ImportSourceComboBox.SelectedItem is not GameSource source) return;

        var dialog = new OpenFileDialog
        {
            Title = $"Import {source} gaming history",
            Filter = source == GameSource.PlayStation
                ? "Supported history files|*.xlsx;*.csv;*.json|Excel files|*.xlsx|CSV files|*.csv|JSON files|*.json"
                : "Supported history files|*.csv;*.json|CSV files|*.csv|JSON files|*.json",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            StatusText.Text = $"Importing {source} history...";
            var result = await _history.ImportFileAsync(source, dialog.FileName);
            StatusText.Text =
                $"Imported {result.StoredCount}/{result.DiscoveredCount} {source} record(s)." +
                (result.Warnings.Count == 0
                    ? string.Empty
                    : $" {result.Warnings.Count} warning(s).");
            await RefreshAsync(keepStatus: true);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async Task RefreshAsync(bool keepStatus = false)
    {
        var items = await _history.GetHistoryAsync();
        HistoryGrid.ItemsSource = items.Select(x => new GamingHistoryViewModel(x)).ToArray();

        if (!keepStatus)
        {
            var accountRecords = items.Sum(x => x.AccountRecords.Count);
            StatusText.Text =
                $"{items.Count} unique game(s) · {accountRecords} imported account record(s) · " +
                $"{items.Count(x => x.IsInstalled)} currently installed.";
        }
    }
}
