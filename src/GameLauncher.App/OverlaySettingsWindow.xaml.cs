using System.Windows;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class OverlaySettingsWindow : Window
{
    private readonly GameplayOverlayService _overlay;

    public OverlaySettingsWindow(GameplayOverlayService overlay)
    {
        InitializeComponent();
        _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));

        IntervalComboBox.ItemsSource = new[]
        {
            new IntervalOption(250, "250 ms"),
            new IntervalOption(500, "500 ms"),
            new IntervalOption(1000, "1 sec"),
            new IntervalOption(2000, "2 sec")
        };

        Loaded += OverlaySettingsWindow_Loaded;
    }

    private async void OverlaySettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OverlaySettingsWindow_Loaded;

        var settings = await _overlay.GetSettingsAsync();
        EnabledCheckBox.IsChecked = settings.Enabled;
        FpsCheckBox.IsChecked = settings.ShowFps;
        GpuUsageCheckBox.IsChecked = settings.ShowGpuUsage;
        GpuTemperatureCheckBox.IsChecked = settings.ShowGpuTemperature;
        IntervalComboBox.SelectedValue = settings.UpdateIntervalMs;

        if (IntervalComboBox.SelectedItem is null)
        {
            IntervalComboBox.SelectedValue = 500;
        }

        var status = await _overlay.GetStatusAsync();
        RtssStatusText.Text = status.Message;
        RtssPathText.Text = status.RivaTunerPath ?? string.Empty;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var interval = IntervalComboBox.SelectedValue is int value ? value : 500;

        await _overlay.SaveSettingsAsync(
            new OverlaySettings(
                EnabledCheckBox.IsChecked == true,
                FpsCheckBox.IsChecked == true,
                GpuUsageCheckBox.IsChecked == true,
                GpuTemperatureCheckBox.IsChecked == true,
                interval));

        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed record IntervalOption(int Value, string Label);
}
