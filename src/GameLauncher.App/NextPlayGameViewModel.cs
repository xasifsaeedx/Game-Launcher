using GameLauncher.Core.Models;

namespace GameLauncher.App;

public sealed record NextPlayGameViewModel(
    PersonalLibraryGame Remote,
    bool IsInstalled,
    int? LocalRating)
{
    public int SourceRow => Remote.SourceRow;
    public int? Rank => Remote.Rank;
    public string RankText => Remote.Rank?.ToString() ?? "—";
    public string Title => Remote.Title;
    public string Platforms => Remote.Platforms;
    public string LibraryStatus => Remote.LibraryStatus switch
    {
        "next" => "Play next",
        "played" => "Played",
        "dislike" => "Didn't like",
        _ => "Unmarked"
    };
    public string Progress => Remote.ProgressStatus switch
    {
        "playing" => "Playing",
        "completed" => "Completed",
        "paused" => "Paused",
        "dropped" => "Dropped",
        _ => "Not started"
    };
    public int? EffectiveRating => LocalRating ?? Remote.SheetRating;
    public string Rating => EffectiveRating.HasValue ? $"{EffectiveRating}/10" : "—";
    public string Installed => IsInstalled ? "Installed" : string.Empty;
}
