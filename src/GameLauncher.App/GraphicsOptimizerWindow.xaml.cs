using System.Windows;
using System.Windows.Controls;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;

namespace GameLauncher.App;

public partial class GraphicsOptimizerWindow : Window
{
    private readonly GraphicsOptimizerService _optimizer;
    private readonly GameLibraryService _library;
    private HardwareProfile? _hardware;
    private IReadOnlyList<GraphicsGameViewModel> _rows = Array.Empty<GraphicsGameViewModel>();
    private TierChoice[] _tierChoices = Array.Empty<TierChoice>();

    public GraphicsOptimizerWindow(
        GraphicsOptimizerService optimizer,
        GameLibraryService library)
    {
        InitializeComponent();
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
        _library = library ?? throw new ArgumentNullException(nameof(library));

        PreferenceComboBox.ItemsSource = Enum.GetValues<GraphicsQualityPreference>();
        _tierChoices =
        [
            new(null, "Auto (detected)"),
            new(GraphicsPerformanceTier.Entry1080p, "Entry 1080p"),
            new(GraphicsPerformanceTier.Mainstream1080p, "Mainstream 1080p"),
            new(GraphicsPerformanceTier.Strong1080p, "Strong 1080p"),
            new(GraphicsPerformanceTier.HighEnd, "High end"),
            new(GraphicsPerformanceTier.Enthusiast, "Enthusiast")
        ];
        TierComboBox.ItemsSource = _tierChoices;

        Loaded += GraphicsOptimizerWindow_Loaded;
    }

    public bool SettingsChanged { get; private set; }

    private async void GraphicsOptimizerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= GraphicsOptimizerWindow_Loaded;
        await LoadStateAsync();
    }

    private async void DetectAgain_Click(object sender, RoutedEventArgs e)
    {
        await LoadHardwareAsync();
        await RefreshGamesAsync();
    }

    private async void SaveTarget_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = BuildSettings();
            await _optimizer.SaveSettingsAsync(settings);
            SettingsChanged = true;
            StatusText.Text = "Graphics target saved.";
            await RefreshGamesAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void GamesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RenderSelection();
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (GamesGrid.SelectedItem is not GraphicsGameViewModel selected) return;

        if (!selected.Recommendation.CanApplyAutomatically)
        {
            StatusText.Text = "This game is recommendation-only; no config file will be edited.";
            return;
        }

        var fields = string.Join(
            Environment.NewLine,
            selected.Recommendation.SafePatches.Select(x => $"• {x.DisplayName}"));

        var confirm = MessageBox.Show(
            $"Game Launcher will change only these verified fields:{Environment.NewLine}{Environment.NewLine}" +
            $"{fields}{Environment.NewLine}{Environment.NewLine}" +
            "The original config is backed up before the first change. Continue?",
            "Apply safe graphics settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            await SaveCurrentSettingsAsync();
            var result = await _optimizer.ApplySafeSettingsAsync(selected.Item);
            StatusText.Text = FormatResult(result);
            await RefreshGamesAsync(selected.GameId);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not apply settings: {ex.Message}";
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (GamesGrid.SelectedItem is not GraphicsGameViewModel selected) return;

        try
        {
            var result = await _optimizer.RestoreOriginalAsync(selected.Item);
            StatusText.Text = FormatResult(result);
            await RefreshGamesAsync(selected.GameId);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not restore settings: {ex.Message}";
        }
    }

    private async Task LoadStateAsync()
    {
        var settings = await _optimizer.GetSettingsAsync();
        WidthTextBox.Text = settings.TargetWidth.ToString();
        HeightTextBox.Text = settings.TargetHeight.ToString();
        FpsTextBox.Text = settings.TargetFps.ToString();
        PreferenceComboBox.SelectedItem = settings.QualityPreference;
        TierComboBox.SelectedItem =
            _tierChoices.First(x => x.Tier == settings.TierOverride);
        AutoApplyCheckBox.IsChecked = settings.AutoApplyBeforeLaunch;

        await LoadHardwareAsync();
        await RefreshGamesAsync();
    }

    private async Task LoadHardwareAsync()
    {
        try
        {
            _hardware = await _optimizer.DetectHardwareAsync();
            HardwareSummaryText.Text =
                $"{_hardware.CpuName}  •  {_hardware.GpuName}  •  {_hardware.RamSummary}  •  " +
                $"{_hardware.DisplaySummary}  •  Detected tier: {FormatTier(_hardware.DetectedTier)}";
        }
        catch (Exception ex)
        {
            HardwareSummaryText.Text = $"Hardware detection unavailable: {ex.Message}";
        }
    }

    private async Task RefreshGamesAsync(Guid? selectGameId = null)
    {
        _hardware ??= await _optimizer.DetectHardwareAsync();
        var settings = BuildSettings().Normalize();
        var games = await _library.GetLibraryAsync();

        _rows = games
            .Select(game => new GraphicsGameViewModel(
                game,
                _optimizer.Recommend(game, settings, _hardware)))
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        GamesGrid.ItemsSource = _rows;
        GamesGrid.SelectedItem = selectGameId.HasValue
            ? _rows.FirstOrDefault(x => x.GameId == selectGameId.Value)
            : _rows.FirstOrDefault();

        if (_rows.Count == 0)
        {
            StatusText.Text = "No installed games are available to optimize.";
        }
    }

    private void RenderSelection()
    {
        if (GamesGrid.SelectedItem is not GraphicsGameViewModel selected)
        {
            SelectedTitleText.Text = string.Empty;
            SelectedSupportText.Text = string.Empty;
            SafeChangesText.Text = "None";
            RecommendationsList.ItemsSource = Array.Empty<string>();
            ApplyButton.IsEnabled = false;
            return;
        }

        SelectedTitleText.Text = selected.Title;
        SelectedSupportText.Text =
            $"Recommended preset: {selected.Preset} · Effective tier: {FormatTier(selected.Recommendation.EffectiveTier)}";

        SafeChangesText.Text = selected.Recommendation.SafePatches.Count == 0
            ? "None — recommendations only."
            : string.Join(
                ", ",
                selected.Recommendation.SafePatches.Select(x => x.DisplayName));

        RecommendationsList.ItemsSource = selected.Recommendation.Recommendations;
        ApplyButton.IsEnabled = selected.Recommendation.CanApplyAutomatically;
    }

    private async Task SaveCurrentSettingsAsync()
    {
        await _optimizer.SaveSettingsAsync(BuildSettings());
        SettingsChanged = true;
    }

    private GraphicsOptimizerSettings BuildSettings()
    {
        if (!int.TryParse(WidthTextBox.Text, out var width) ||
            !int.TryParse(HeightTextBox.Text, out var height) ||
            !int.TryParse(FpsTextBox.Text, out var fps))
        {
            throw new InvalidOperationException("Width, height and FPS must be whole numbers.");
        }

        var preference = PreferenceComboBox.SelectedItem is GraphicsQualityPreference selectedPreference
            ? selectedPreference
            : GraphicsQualityPreference.Quality;

        var tier = TierComboBox.SelectedItem is TierChoice selectedTier
            ? selectedTier.Tier
            : null;

        return new GraphicsOptimizerSettings(
            width,
            height,
            fps,
            preference,
            tier,
            AutoApplyCheckBox.IsChecked == true).Normalize();
    }

    private static string FormatResult(GraphicsApplyResult result)
    {
        var parts = new List<string>();

        if (result.AppliedChanges.Count > 0)
        {
            parts.Add(string.Join(" | ", result.AppliedChanges));
        }

        if (result.Warnings.Count > 0)
        {
            parts.Add(string.Join(" ", result.Warnings));
        }

        if (parts.Count == 0)
        {
            parts.Add(result.Changed ? "Graphics configuration updated." : "No graphics changes were needed.");
        }

        return string.Join(" ", parts);
    }

    private static string FormatTier(GraphicsPerformanceTier tier) => tier switch
    {
        GraphicsPerformanceTier.Entry1080p => "Entry 1080p",
        GraphicsPerformanceTier.Mainstream1080p => "Mainstream 1080p",
        GraphicsPerformanceTier.Strong1080p => "Strong 1080p",
        GraphicsPerformanceTier.HighEnd => "High end",
        GraphicsPerformanceTier.Enthusiast => "Enthusiast",
        _ => "Unknown"
    };

    private sealed record TierChoice(
        GraphicsPerformanceTier? Tier,
        string Label);
}
