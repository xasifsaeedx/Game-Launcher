using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Sync;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public sealed class HatchableSyncService
{
    private readonly IHatchableSyncRepository _repository;
    private readonly IHatchableApiClient _client;
    private readonly GameLibraryService _library;

    public HatchableSyncService(
        IHatchableSyncRepository repository,
        IHatchableApiClient client,
        GameLibraryService library)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public Task<HatchableSyncSettings?> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetSettingsAsync(cancellationToken);

    public async Task SaveSettingsAsync(
        HatchableSyncSettings settings,
        bool validateConnection = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();

        if (!settings.IsConfigured)
        {
            throw new ArgumentException(
                "A valid Hatchable URL and launcher token are required.",
                nameof(settings));
        }

        if (validateConnection)
        {
            await _client.GetGamesAsync(settings, cancellationToken);
        }

        await _repository.SaveSettingsAsync(settings, cancellationToken);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default) =>
        _repository.ClearSettingsAsync(cancellationToken);

    public async Task<bool> IsAutoSyncEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetSettingsAsync(cancellationToken);
        return settings is { AutoSync: true, IsConfigured: true };
    }

    public async Task<HatchableSyncResult> SyncAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = (await _repository.GetSettingsAsync(cancellationToken))?.Normalize()
            ?? throw new InvalidOperationException("Hatchable Sync is not connected.");

        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("Hatchable Sync is not connected.");
        }

        var remote = await _client.GetGamesAsync(settings, cancellationToken);
        var local = await _library.GetLibraryAsync(cancellationToken);

        var pushes = new List<HatchableGamePush>();
        var matched = 0;

        foreach (var localGame in local)
        {
            var match = FindMatch(localGame, remote);
            if (match is null) continue;

            matched++;
            var progress = match.ProgressStatus;
            if (progress is null &&
                localGame.TotalPlaytimeSeconds > 0 &&
                match.LibraryStatus is not ("played" or "dislike"))
            {
                progress = "playing";
            }

            pushes.Add(new HatchableGamePush(
                match.RemoteGameId,
                localGame.TotalPlaytimeSeconds,
                localGame.LastPlayedUtc,
                progress,
                match.Rating));
        }

        var pushed = pushes.Count == 0
            ? 0
            : await _client.PushGamesAsync(settings, pushes, cancellationToken);

        var refreshed = pushed > 0
            ? await _client.GetGamesAsync(settings, cancellationToken)
            : remote;

        await _repository.ReplaceRemoteGamesAsync(refreshed, cancellationToken);

        return new HatchableSyncResult(
            refreshed.Count,
            matched,
            pushed,
            Math.Max(0, refreshed.Count - matched),
            Array.Empty<string>());
    }

    public Task<IReadOnlyList<HatchableRemoteGame>> GetCachedGamesAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetRemoteGamesAsync(cancellationToken);

    public async Task<HatchableRemoteGame> UpdateRemoteStateAsync(
        int remoteGameId,
        string? progressStatus,
        int? rating,
        CancellationToken cancellationToken = default)
    {
        if (remoteGameId < 1) throw new ArgumentOutOfRangeException(nameof(remoteGameId));

        var allowed = new HashSet<string?>(StringComparer.OrdinalIgnoreCase)
        {
            null, "playing", "completed", "paused", "dropped"
        };
        if (!allowed.Contains(progressStatus))
        {
            throw new ArgumentException("Invalid progress status.", nameof(progressStatus));
        }

        if (rating is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be 1-10.");
        }

        var settings = (await _repository.GetSettingsAsync(cancellationToken))?.Normalize()
            ?? throw new InvalidOperationException("Hatchable Sync is not connected.");

        var cached = await _repository.GetRemoteGamesAsync(cancellationToken);
        var game = cached.FirstOrDefault(x => x.RemoteGameId == remoteGameId)
            ?? throw new InvalidOperationException("Remote game is not in the local sync cache.");

        await _client.PushGamesAsync(
            settings,
            new[]
            {
                new HatchableGamePush(
                    game.RemoteGameId,
                    game.PlaytimeSeconds,
                    game.LastPlayedAt,
                    progressStatus,
                    rating)
            },
            cancellationToken);

        var refreshed = await _client.GetGamesAsync(settings, cancellationToken);
        await _repository.ReplaceRemoteGamesAsync(refreshed, cancellationToken);

        return refreshed.First(x => x.RemoteGameId == remoteGameId);
    }

    public async Task<HatchableRemoteGame?> GetCachedMatchAsync(
        GameLibraryItem item,
        CancellationToken cancellationToken = default)
    {
        var remote = await _repository.GetRemoteGamesAsync(cancellationToken);
        return FindMatch(item, remote);
    }

    public static HatchableRemoteGame? FindMatch(
        GameLibraryItem local,
        IReadOnlyList<HatchableRemoteGame> remoteGames)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remoteGames);

        var steamIds = local.Installations
            .Where(x => x.Source == GameSource.Steam)
            .Select(x => x.ExternalId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var steamMatch = remoteGames.FirstOrDefault(remote =>
            remote.SteamAppId.HasValue &&
            steamIds.Contains(remote.SteamAppId.Value.ToString()));

        if (steamMatch is not null) return steamMatch;

        var normalized = GameTitleNormalizer.Normalize(local.Game.Title);
        return remoteGames.FirstOrDefault(remote =>
            string.Equals(
                GameTitleNormalizer.Normalize(remote.Title),
                normalized,
                StringComparison.Ordinal));
    }
}
