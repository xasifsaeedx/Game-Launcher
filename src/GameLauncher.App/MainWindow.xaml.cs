using System.Windows;
using GameLauncher.Core.Services;
using GameLauncher.Infrastructure.Storage;

namespace GameLauncher.App;

public partial class MainWindow : Window
{
    private readonly GameLibraryService _library;

    public MainWindow(GameLibraryService library, AppPaths paths)
    {
        InitializeComponent();
        _library = library ?? throw new ArgumentNullException(nameof(library));
        ArgumentNullException.ThrowIfNull(paths);

        DatabasePathText.Text = paths.DatabasePath;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await RunFoundationCheckAsync();
    }

    private async void FoundationCheckButton_Click(object sender, RoutedEventArgs e)
    {
        await RunFoundationCheckAsync();
    }

    private async Task RunFoundationCheckAsync()
    {
        FoundationCheckButton.IsEnabled = false;
        StatusText.Text = "Checking database and launcher adapter...";

        try
        {
            var result = await _library.DiscoverAndStoreAsync();
            var games = await _library.GetGamesAsync();
            GamesList.ItemsSource = games;

            StatusText.Text =
                $"Ready. {result.AdapterCount} adapter(s), {result.DiscoveredCount} discovered, " +
                $"{games.Count} game(s) stored.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Foundation check failed: {ex.Message}";
        }
        finally
        {
            FoundationCheckButton.IsEnabled = true;
        }
    }
}
