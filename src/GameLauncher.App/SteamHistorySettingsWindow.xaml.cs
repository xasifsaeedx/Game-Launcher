using System.Diagnostics;
using System.Windows;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class SteamHistorySettingsWindow : Window
{
    private readonly GameHistoryService _history;

    public SteamHistorySettingsWindow(GameHistoryService history)
    {
        InitializeComponent();
        _history = history ?? throw new ArgumentNullException(nameof(history));
        Loaded += SteamHistorySettingsWindow_Loaded;
    }

    public bool Changed { get; private set; }

    private async void SteamHistorySettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= SteamHistorySettingsWindow_Loaded;

        var settings = await _history.GetSteamSettingsAsync();
        SteamIdTextBox.Text = settings?.SteamId64 ?? string.Empty;
        ApiKeyPasswordBox.Password = settings?.ApiKey ?? string.Empty;
        AutoSyncCheckBox.IsChecked = settings?.AutoSync ?? true;
    }

    private void OpenApiKeyPage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://steamcommunity.com/dev/apikey")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Testing Steam connection...";
            var settings = new SteamHistorySettings(
                SteamIdTextBox.Text,
                ApiKeyPasswordBox.Password,
                AutoSyncCheckBox.IsChecked == true);

            await _history.SaveSteamSettingsAsync(settings, validate: true);
            var result = await _history.SyncSteamAsync();

            StatusText.Text =
                $"Connected. Imported {result.StoredCount} Steam game(s). " +
                "The API key is encrypted for this Windows user.";
            Changed = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await _history.DisconnectSteamAsync();
        SteamIdTextBox.Text = string.Empty;
        ApiKeyPasswordBox.Password = string.Empty;
        StatusText.Text = "Steam API credentials removed from this PC. Imported history is retained.";
        Changed = true;
    }
}
