using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Sync;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public sealed class PersonalLibraryService
{
    private readonly IPersonalLibraryRepository _repository;
    private readonly IPersonalLibraryClient _client;
    private readonly GameLibraryService _library;

    public PersonalLibraryService(
        IPersonalLibraryRepository repository,
        IPersonalLibraryClient client,
        GameLibraryService library)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public Task<PersonalLibrarySettings?> GetSettingsAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetSettingsAsync(cancellationToken);

    public async Task SaveSettingsAsync(
        PersonalLibrarySettings settings,
        bool validateConnection = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();

        if (!settings.IsConfigured)
        {
            throw new ArgumentException(
                "Enter a valid Google Sheets URL from docs.google.com.",
                nameof(settings));
        }

        if (validateConnection)
        {
            _ = await _client.GetGamesAsync(settings, cancellationToken);
        }

        await _repository.SaveSettingsAsync(settings, cancellationToken);
    }

    public async Task DisconnectAsync(
        CancellationToken cancellationToken = default)
    {
        await _repository.ClearSettingsAsync(cancellationToken);
        await _repository.ReplaceGamesAsync(
            Array.Empty<PersonalLibraryGame>(),
            cancellationToken);
    }

    public async Task<bool> IsAutoSyncEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetSettingsAsync(cancellationToken);
        return settings is { AutoSync: true, IsConfigured: true };
    }

    public async Task<PersonalLibrarySyncResult> SyncAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = (await _repository.GetSettingsAsync(cancellationToken))?.Normalize()
            ?? throw new InvalidOperationException("Personal Game Library is not connected.");

        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("Personal Game Library is not connected.");
        }

        var remote = await _client.GetGamesAsync(settings, cancellationToken);
        await _repository.ReplaceGamesAsync(remote, cancellationToken);

        var local = await _library.GetLibraryAsync(cancellationToken);
        var matched = local.Count(item => FindMatch(item, remote) is not null);

        return new PersonalLibrarySyncResult(remote.Count, matched);
    }

    public Task<IReadOnlyList<PersonalLibraryGame>> GetCachedGamesAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetGamesAsync(cancellationToken);

    public static PersonalLibraryGame? FindMatch(
        GameLibraryItem local,
        IReadOnlyList<PersonalLibraryGame> remoteGames)
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
