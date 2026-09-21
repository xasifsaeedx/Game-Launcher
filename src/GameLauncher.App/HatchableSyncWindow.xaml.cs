using System.Diagnostics;
using System.Windows;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class HatchableSyncWindow : Window
{
    private readonly HatchableSyncService _sync;

    public HatchableSyncWindow(HatchableSyncService sync)
    {
        InitializeComponent();
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        Loaded += HatchableSyncWindow_Loaded;
    }

    public bool Changed { get; private set; }

    private async void HatchableSyncWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HatchableSyncWindow_Loaded;

        var settings = await _sync.GetSettingsAsync();
        BaseUrlTextBox.Text = settings?.BaseUrl ?? HatchableSyncSettings.DefaultBaseUrl;
        TokenPasswordBox.Password = settings?.Token ?? string.Empty;
        AutoSyncCheckBox.IsChecked = settings?.AutoSync ?? true;
        ConnectionText.Text = settings?.IsConfigured == true
            ? "Connected."
            : "Not connected.";
    }

    private void OpenPairingPage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseUrl = NormalizeBaseUrl();
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                StatusText.Text = "Hatchable Sync requires an HTTPS site URL.";
                return;
            }

            Process.Start(new ProcessStartInfo(baseUrl + "/launcher")
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
            StatusText.Text = "Testing connection...";
            await _sync.SaveSettingsAsync(BuildSettings(), validateConnection: true);
            ConnectionText.Text = "Connected.";
            StatusText.Text = "Connection saved. The launcher token is encrypted for this Windows user.";
            Changed = true;
        }
        catch (Exception ex)
        {
            ConnectionText.Text = "Not connected.";
            StatusText.Text = ex.Message;
        }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsureSettingsSavedAsync();
            StatusText.Text = "Syncing...";
            var result = await _sync.SyncAsync();
            StatusText.Text =
                $"Synced {result.RemoteCount} ranked games; matched {result.MatchedCount} installed game(s).";
            Changed = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await _sync.DisconnectAsync();
        TokenPasswordBox.Password = string.Empty;
        ConnectionText.Text = "Not connected.";
        StatusText.Text = "Local Hatchable credentials removed. Revoke the device token on the website too.";
        Changed = true;
    }

    private async Task EnsureSettingsSavedAsync()
    {
        var current = await _sync.GetSettingsAsync();
        var proposed = BuildSettings();

        if (current is null ||
            !string.Equals(current.BaseUrl, proposed.BaseUrl, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(current.Token, proposed.Token, StringComparison.Ordinal) ||
            current.AutoSync != proposed.AutoSync)
        {
            await _sync.SaveSettingsAsync(proposed, validateConnection: true);
        }
    }

    private HatchableSyncSettings BuildSettings() =>
        new(
            NormalizeBaseUrl(),
            TokenPasswordBox.Password,
            AutoSyncCheckBox.IsChecked == true);

    private string NormalizeBaseUrl()
    {
        var value = BaseUrlTextBox.Text.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? HatchableSyncSettings.DefaultBaseUrl
            : value.TrimEnd('/');
    }
}
