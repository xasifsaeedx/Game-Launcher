using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Sync;

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

        var normalized = settings.Normalize();
        if (!normalized.IsConfigured)
        {
            throw new ArgumentException(
                "Enter a valid Google Sheets URL from docs.google.com.",
                nameof(settings));
        }

        if (validateConnection)
        {
            _ = await _client.GetGamesAsync(
                normalized,
                cancellationToken);
        }

        await _repository.SaveSettingsAsync(
            normalized,
            cancellationToken);
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
        var settings = await GetConfiguredSettingsAsync(cancellationToken);
        var personalGames = await _client.GetGamesAsync(
            settings,
            cancellationToken);

        await _repository.ReplaceGamesAsync(
            personalGames,
            cancellationToken);

        var localGames = await _library.GetLibraryAsync(cancellationToken);
        var matchedCount = localGames.Count(localGame =>
            PersonalLibraryMatcher.FindMatch(
                localGame,
                personalGames) is not null);

        return new PersonalLibrarySyncResult(
            personalGames.Count,
            matchedCount);
    }

    public Task<IReadOnlyList<PersonalLibraryGame>> GetCachedGamesAsync(
        CancellationToken cancellationToken = default) =>
        _repository.GetGamesAsync(cancellationToken);

    private async Task<PersonalLibrarySettings> GetConfiguredSettingsAsync(
        CancellationToken cancellationToken)
    {
        var settings = (await _repository.GetSettingsAsync(cancellationToken))
            ?.Normalize();

        if (settings is not { IsConfigured: true })
        {
            throw new InvalidOperationException(
                "Personal Game Library is not connected.");
        }

        return settings;
    }
}
