using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;
using Microsoft.Win32;

namespace GameLauncher.App;

public partial class LaunchProfileWindow : Window
{
    private readonly GameLibraryItem _item;
    private readonly LaunchProfileService _profiles;
    private readonly ObservableCollection<LaunchActionRow> _actions = new();
    private LaunchProfile? _current;
    private bool _loading;

    public LaunchProfileWindow(GameLibraryItem item, LaunchProfileService profiles)
    {
        InitializeComponent();
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));

        HeadingText.Text = item.Game.Title;
        ActionsGrid.ItemsSource = _actions;

        var installations = new List<InstallationOption> { InstallationOption.Automatic };
        installations.AddRange(item.Installations.Select(InstallationOption.FromInstallation));
        InstallationComboBox.ItemsSource = installations;
        InstallationComboBox.SelectedIndex = 0;

        Loaded += LaunchProfileWindow_Loaded;
    }

    private async void LaunchProfileWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= LaunchProfileWindow_Loaded;
        await ReloadProfilesAsync();
    }

    private async void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ProfilesList.SelectedItem is not LaunchProfile profile) return;
        await LoadProfileAsync(profile.Id);
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        _current = new LaunchProfile(
            Guid.NewGuid(),
            _item.Game.Id,
            "Gaming",
            null,
            null,
            ProfilesList.Items.Count == 0,
            now,
            now);

        ProfilesList.SelectedItem = null;
        ProfileNameTextBox.Text = _current.Name;
        InstallationComboBox.SelectedIndex = 0;
        GameArgumentsTextBox.Text = string.Empty;
        DefaultCheckBox.IsChecked = _current.IsDefault;
        _actions.Clear();
        StatusText.Text = "New profile. Add programs or save it as-is.";
    }

    private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;

        var answer = MessageBox.Show(
            this,
            $"Delete profile '{_current.Name}'?",
            "Launch profiles",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer != MessageBoxResult.Yes) return;

        await _profiles.DeleteAsync(_current.Id);
        _current = null;
        await ReloadProfilesAsync();
        StatusText.Text = "Profile deleted.";
    }

    private void AddAction_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) NewProfile_Click(sender, e);

        var stage = sender is Button button &&
                    Enum.TryParse<LaunchActionStage>(button.Tag?.ToString(), out var parsed)
            ? parsed
            : LaunchActionStage.Companion;

        var dialog = new OpenFileDialog
        {
            Title = stage switch
            {
                LaunchActionStage.PreLaunch => "Choose pre-launch program",
                LaunchActionStage.PostGame => "Choose post-game program",
                _ => "Choose companion program"
            },
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true) return;

        _actions.Add(new LaunchActionRow
        {
            Stage = stage,
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            ExecutablePath = dialog.FileName,
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName),
            SortOrder = _actions.Count,
            WaitForExit = stage == LaunchActionStage.PreLaunch,
            CloseWithGame = stage == LaunchActionStage.Companion,
            IsEnabled = true
        });
    }

    private async void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) NewProfile_Click(sender, e);
        if (_current is null) return;

        if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text))
        {
            MessageBox.Show(this, "Enter a profile name.", "Launch profiles");
            return;
        }

        foreach (var action in _actions)
        {
            if (string.IsNullOrWhiteSpace(action.Name) ||
                string.IsNullOrWhiteSpace(action.ExecutablePath))
            {
                MessageBox.Show(
                    this,
                    "Every action needs a name and program path.",
                    "Launch profiles");
                return;
            }
        }

        var selectedInstall = InstallationComboBox.SelectedItem as InstallationOption;
        var now = DateTimeOffset.UtcNow;
        var profile = _current with
        {
            Name = ProfileNameTextBox.Text.Trim(),
            InstallationId = selectedInstall?.Id,
            GameArgumentsOverride = NormalizeOptional(GameArgumentsTextBox.Text),
            IsDefault = DefaultCheckBox.IsChecked == true,
            UpdatedUtc = now
        };

        var models = _actions
            .Select((row, index) =>
            {
                row.SortOrder = index;
                return row.ToModel(profile.Id);
            })
            .ToArray();

        try
        {
            await _profiles.SaveAsync(_item, profile, models);
            _current = profile;
            await ReloadProfilesAsync(profile.Id);
            StatusText.Text = "Profile saved.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not save profile: {ex.Message}";
        }
    }

    private async Task ReloadProfilesAsync(Guid? selectProfileId = null)
    {
        _loading = true;
        try
        {
            var details = await _profiles.GetProfilesAsync(_item);
            var profiles = details.Select(x => x.Profile).ToArray();
            ProfilesList.ItemsSource = profiles;

            var target = selectProfileId.HasValue
                ? profiles.FirstOrDefault(x => x.Id == selectProfileId.Value)
                : profiles.FirstOrDefault(x => x.IsDefault) ?? profiles.FirstOrDefault();

            if (target is null)
            {
                NewProfile_Click(this, new RoutedEventArgs());
                return;
            }

            ProfilesList.SelectedItem = target;
            await LoadProfileAsync(target.Id);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task LoadProfileAsync(Guid profileId)
    {
        var details = await _profiles.GetProfileAsync(profileId);
        if (details is null) return;

        _current = details.Profile;
        ProfileNameTextBox.Text = details.Profile.Name;
        GameArgumentsTextBox.Text = details.Profile.GameArgumentsOverride ?? string.Empty;
        DefaultCheckBox.IsChecked = details.Profile.IsDefault;

        InstallationComboBox.SelectedItem =
            ((IEnumerable<InstallationOption>)InstallationComboBox.ItemsSource)
            .FirstOrDefault(x => x.Id == details.Profile.InstallationId)
            ?? InstallationOption.Automatic;

        _actions.Clear();
        foreach (var action in details.Actions)
        {
            _actions.Add(LaunchActionRow.FromModel(action));
        }

        StatusText.Text = details.Profile.IsDefault
            ? "Default one-click profile."
            : "Profile loaded.";
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
