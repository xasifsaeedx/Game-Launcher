namespace GameLauncher.Core.Models;

public sealed record SourceSyncResult(
    int AdapterCount,
    int DiscoveredCount,
    int StoredCount);
