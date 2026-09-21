using GameLauncher.Core.Models;

namespace GameLauncher.App;

public sealed record NextPlayGameViewModel(
    HatchableRemoteGame Remote,
    bool IsInstalled)
{
    public int RemoteGameId => Remote.RemoteGameId;
    public int Rank => Remote.RankScore;
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
    public string Rating => Remote.Rating.HasValue ? $"{Remote.Rating}/10" : "—";
    public string Playtime => GameCardViewModel.FormatPlaytime(Remote.PlaytimeSeconds);
    public string Installed => IsInstalled ? "Installed" : string.Empty;
}
