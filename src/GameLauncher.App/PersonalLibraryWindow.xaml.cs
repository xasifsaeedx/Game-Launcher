using System.Diagnostics;
using System.Windows;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class PersonalLibraryWindow : Window
{
    private readonly PersonalLibraryService _library;

    public PersonalLibraryWindow(PersonalLibraryService library)
    {
        InitializeComponent();
        _library = library ?? throw new ArgumentNullException(nameof(library));
        Loaded += PersonalLibraryWindow_Loaded;
    }

    public bool Changed { get; private set; }

    private async void PersonalLibraryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PersonalLibraryWindow_Loaded;
        var settings = await _library.GetSettingsAsync();
        SheetUrlTextBox.Text = settings?.SheetUrl ?? string.Empty;
        AutoSyncCheckBox.IsChecked = settings?.AutoSync ?? true;
        ConnectionText.Text = settings?.IsConfigured == true
            ? "Google Sheets library linked."
            : "Not connected.";
    }

    private void OpenSheet_Click(object sender, RoutedEventArgs e)
    {
        if (!Uri.TryCreate(SheetUrlTextBox.Text.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            StatusText.Text = "Enter a valid Google Sheets URL first.";
            return;
        }

        Process.Start(new ProcessStartInfo(uri.ToString())
        {
            UseShellExecute = true
        });
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Testing Google Sheets access...";
            await _library.SaveSettingsAsync(BuildSettings(), validateConnection: true);
            ConnectionText.Text = "Google Sheets library linked.";
            StatusText.Text = "Saved. The launcher can now refresh your personal game library.";
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
            await _library.SaveSettingsAsync(BuildSettings(), validateConnection: false);
            StatusText.Text = "Refreshing personal game library...";
            var result = await _library.SyncAsync();
            ConnectionText.Text = "Google Sheets library linked.";
            StatusText.Text =
                $"Imported {result.RemoteCount} game(s); matched {result.MatchedCount} installed game(s).";
            Changed = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await _library.DisconnectAsync();
        SheetUrlTextBox.Text = string.Empty;
        ConnectionText.Text = "Not connected.";
        StatusText.Text = "Personal library link and cached sheet data removed.";
        Changed = true;
    }

    private PersonalLibrarySettings BuildSettings() =>
        new(
            SheetUrlTextBox.Text.Trim(),
            AutoSyncCheckBox.IsChecked == true);
}
