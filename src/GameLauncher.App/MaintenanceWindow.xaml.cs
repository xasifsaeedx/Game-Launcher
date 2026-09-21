using System.Diagnostics;
using System.Reflection;
using System.Windows;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;
using GameLauncher.Core.Update;
using GameLauncher.Infrastructure.Backup;
using GameLauncher.Infrastructure.Storage;
using Microsoft.Win32;

namespace GameLauncher.App;

public partial class MaintenanceWindow : Window
{
    private readonly AppPaths _paths;
    private readonly LauncherBackupService _backup;
    private readonly GameHistoryService _history;
    private readonly ILauncherUpdateService _updates;
    private LauncherUpdateInfo? _availableUpdate;

    public MaintenanceWindow(
        AppPaths paths,
        LauncherBackupService backup,
        GameHistoryService history,
        ILauncherUpdateService updates)
    {
        InitializeComponent();
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _backup = backup ?? throw new ArgumentNullException(nameof(backup));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _updates = updates ?? throw new ArgumentNullException(nameof(updates));

        VersionText.Text = $"Installed version: {CurrentVersion}";
    }

    private static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Create Game Launcher backup",
            Filter = "ZIP backup|*.zip",
            FileName = $"GameLauncher-Backup-{DateTime.Now:yyyyMMdd-HHmm}.zip",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            StatusText.Text = "Creating backup...";
            await _backup.CreateBackupAsync(_paths, dialog.FileName);
            StatusText.Text = $"Backup created: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Backup failed: {ex.Message}";
        }
    }

    private async void ExportHistory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Gaming History",
            Filter = "CSV file|*.csv",
            FileName = $"GameLauncher-History-{DateTime.Now:yyyyMMdd}.csv",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true) return;

        try
        {
            StatusText.Text = "Exporting Gaming History...";
            var history = await _history.GetHistoryAsync();
            await _backup.ExportHistoryCsvAsync(history, dialog.FileName);
            StatusText.Text = $"History exported: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            InstallUpdateButton.IsEnabled = false;
            StatusText.Text = "Checking GitHub releases...";

            var update = await _updates.CheckAsync(CurrentVersion);
            _availableUpdate = update.IsUpdateAvailable ? update : null;

            if (!update.IsUpdateAvailable)
            {
                StatusText.Text = $"You are up to date. Latest release: v{update.LatestVersion}.";
                return;
            }

            InstallUpdateButton.IsEnabled = update.InstallerDownload is not null;
            StatusText.Text = update.InstallerDownload is null
                ? $"v{update.LatestVersion} is available, but that release has no installer asset."
                : $"v{update.LatestVersion} is available: {update.ReleaseName}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Update check failed: {ex.Message}";
        }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_availableUpdate is null) return;

        var confirm = MessageBox.Show(
            $"Download and start Game Launcher v{_availableUpdate.LatestVersion} installer?\n\n" +
            "The launcher will close after the installer starts.",
            "Install update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            InstallUpdateButton.IsEnabled = false;
            StatusText.Text = "Downloading installer...";
            var installer = await _updates.DownloadInstallerAsync(_availableUpdate);
            Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            InstallUpdateButton.IsEnabled = true;
            StatusText.Text = $"Update download failed: {ex.Message}";
        }
    }
}
