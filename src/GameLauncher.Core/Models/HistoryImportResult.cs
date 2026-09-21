namespace GameLauncher.Core.Models;

public sealed record HistoryImportResult(
    GameSource Source,
    int DiscoveredCount,
    int StoredCount,
    IReadOnlyList<string> Warnings);
