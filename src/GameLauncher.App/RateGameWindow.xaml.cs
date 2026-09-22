using System.Windows;

namespace GameLauncher.App;

public partial class RateGameWindow : Window
{
    public RateGameWindow(string gameTitle, int? currentRating)
    {
        InitializeComponent();
        GameTitleText.Text = gameTitle;
        RatingComboBox.ItemsSource = new[] { "Not rated" }
            .Concat(Enumerable.Range(1, 10).Reverse().Select(x => $"{x} / 10"))
            .ToArray();

        RatingComboBox.SelectedIndex = currentRating.HasValue
            ? 11 - currentRating.Value
            : 0;
    }

    public int? Rating { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Rating = RatingComboBox.SelectedIndex == 0
            ? null
            : 11 - RatingComboBox.SelectedIndex;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;
}
