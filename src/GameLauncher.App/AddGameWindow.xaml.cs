using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace GameLauncher.App;

public partial class AddGameWindow : Window
{
    public AddGameWindow()
    {
        InitializeComponent();
    }

    public string GameTitle => TitleTextBox.Text.Trim();
    public string ExecutablePath => ExecutableTextBox.Text.Trim();
    public string? CoverImagePath => NormalizeOptional(CoverTextBox.Text);
    public string? LaunchArguments => NormalizeOptional(ArgumentsTextBox.Text);

    private void BrowseExecutable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose game executable",
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true) return;
        ExecutableTextBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
        {
            TitleTextBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
        }
    }

    private void BrowseCover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose cover image",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true) CoverTextBox.Text = dialog.FileName;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GameTitle))
        {
            MessageBox.Show(this, "Enter a game name.", "Add game", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(ExecutablePath) || !File.Exists(ExecutablePath))
        {
            MessageBox.Show(this, "Choose an existing game executable.", "Add game", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (CoverImagePath is not null && !File.Exists(CoverImagePath))
        {
            MessageBox.Show(this, "The selected cover image does not exist.", "Add game", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string? NormalizeOptional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
