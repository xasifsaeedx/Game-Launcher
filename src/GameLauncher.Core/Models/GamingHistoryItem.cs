namespace GameLauncher.Core.Models;

public sealed record GamingHistoryItem(
    string Title,
    IReadOnlyList<GameHistoryRecord> AccountRecords,
    bool IsInstalled,
    long LauncherTrackedSeconds,
    DateTimeOffset? LauncherLastPlayedUtc)
{
    public long KnownAccountPlaytimeSeconds =>
        AccountRecords.Where(x => x.PlaytimeSeconds.HasValue)
            .Sum(x => x.PlaytimeSeconds!.Value);

    public DateTimeOffset? LastPlayedUtc =>
        AccountRecords.Where(x => x.LastPlayedUtc.HasValue)
            .Select(x => x.LastPlayedUtc)
            .Append(LauncherLastPlayedUtc)
            .Where(x => x.HasValue)
            .Max();

    public IReadOnlyList<GameSource> Sources =>
        AccountRecords.Select(x => x.Source).Distinct().OrderBy(x => x).ToArray();
}
