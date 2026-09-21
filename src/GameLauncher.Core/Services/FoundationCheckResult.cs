namespace GameLauncher.Core.Services;

public sealed record FoundationCheckResult(
    int AdapterCount,
    int DiscoveredCount,
    int StoredCount);
