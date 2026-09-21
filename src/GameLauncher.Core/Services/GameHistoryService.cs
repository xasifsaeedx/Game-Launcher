using GameLauncher.Core.History;
using GameLauncher.Core.Models;
using GameLauncher.Core.Repositories;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Core.Services;

public sealed class GameHistoryService
{
    private readonly IGameHistoryRepository _history;
    private readonly ISteamHistorySettingsRepository _steamSettings;
    private readonly ISteamHistoryClient _steam;
    private readonly IReadOnlyList<IHistoryFileParser> _fileParsers;
    private readonly GameLibraryService _library;

    public GameHistoryService(
        IGameHistoryRepository history,
        ISteamHistorySettingsRepository steamSettings,
        ISteamHistoryClient steam,
        IEnumerable<IHistoryFileParser> fileParsers,
        GameLibraryService library)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _steamSettings = steamSettings ?? throw new ArgumentNullException(nameof(steamSettings));
        _steam = steam ?? throw new ArgumentNullException(nameof(steam));
        _fileParsers = fileParsers?.ToArray() ?? throw new ArgumentNullException(nameof(fileParsers));
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public Task<SteamHistorySettings?> GetSteamSettingsAsync(
        CancellationToken cancellationToken = default) =>
        _steamSettings.GetAsync(cancellationToken);

    public async Task SaveSteamSettingsAsync(
        SteamHistorySettings settings,
        bool validate = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();

        if (!settings.IsConfigured)
        {
            throw new ArgumentException(
                "A numeric SteamID64 and Steam Web API key are required.",
                nameof(settings));
        }

        if (validate)
        {
            _ = await _steam.GetOwnedGamesAsync(settings, cancellationToken);
        }

        await _steamSettings.SaveAsync(settings, cancellationToken);
    }

    public Task DisconnectSteamAsync(
        CancellationToken cancellationToken = default) =>
        _steamSettings.ClearAsync(cancellationToken);

    public async Task<bool> IsSteamAutoSyncEnabledAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await _steamSettings.GetAsync(cancellationToken);
        return settings is { AutoSync: true, IsConfigured: true };
    }

    public async Task<HistoryImportResult> SyncSteamAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = (await _steamSettings.GetAsync(cancellationToken))?.Normalize()
            ?? throw new InvalidOperationException("Steam history is not connected.");

        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("Steam history is not connected.");
        }

        var games = await _steam.GetOwnedGamesAsync(settings, cancellationToken);
        return await StoreAsync(GameSource.Steam, games, cancellationToken);
    }

    public async Task<HistoryImportResult> ImportFileAsync(
        GameSource source,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (source is GameSource.Unknown or GameSource.Manual or GameSource.Demo)
        {
            throw new ArgumentException("Choose the account/store represented by this file.", nameof(source));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("History import file was not found.", fullPath);
        }

        var parser = _fileParsers.FirstOrDefault(x => x.CanParse(source, fullPath))
            ?? throw new NotSupportedException(
                $"No Phase 7 importer supports '{Path.GetExtension(fullPath)}' for {source}.");

        var games = await parser.ParseAsync(source, fullPath, cancellationToken);
        return await StoreAsync(source, games, cancellationToken);
    }

    public async Task<IReadOnlyList<GamingHistoryItem>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        await _history.InitializeAsync(cancellationToken);

        var accountRecords = await _history.GetAllAsync(cancellationToken);
        var localLibrary = await _library.GetLibraryAsync(cancellationToken);

        var accountGroups = accountRecords
            .GroupBy(x => GameTitleNormalizer.Normalize(x.Title))
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        var localGroups = localLibrary
            .GroupBy(x => GameTitleNormalizer.Normalize(x.Game.Title))
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        var keys = accountGroups.Keys
            .Concat(localGroups.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var output = new List<GamingHistoryItem>(keys.Length);

        foreach (var key in keys)
        {
            accountGroups.TryGetValue(key, out var records);
            localGroups.TryGetValue(key, out var locals);
            records ??= Array.Empty<GameHistoryRecord>();
            locals ??= Array.Empty<GameLibraryItem>();

            var title = locals.FirstOrDefault()?.Game.Title
                ?? records.FirstOrDefault()?.Title
                ?? key;

            output.Add(
                new GamingHistoryItem(
                    title,
                    records,
                    locals.Any(x => x.Installations.Any(i => i.IsInstalled)),
                    locals.Sum(x => x.TotalPlaytimeSeconds),
                    locals.Where(x => x.LastPlayedUtc.HasValue)
                        .Select(x => x.LastPlayedUtc)
                        .Max()));
        }

        return output
            .OrderByDescending(x => x.LastPlayedUtc.HasValue)
            .ThenByDescending(x => x.LastPlayedUtc)
            .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<HistoryImportResult> StoreAsync(
        GameSource expectedSource,
        IReadOnlyList<HistoryImportGame> games,
        CancellationToken cancellationToken)
    {
        await _history.InitializeAsync(cancellationToken);

        var warnings = new List<string>();
        var stored = 0;

        var unique = games
            .Where(x => x.Source == expectedSource)
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .GroupBy(
                x => NormalizeExternalId(x),
                StringComparer.OrdinalIgnoreCase)
            .Select(x => x
                .OrderByDescending(g => g.PlaytimeSeconds ?? -1)
                .ThenByDescending(g => g.LastPlayedUtc)
                .First())
            .ToArray();

        foreach (var game in unique)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var externalId = NormalizeExternalId(game);
                var record = new GameHistoryRecord(
                    StableId.FromText($"history:{expectedSource}:{externalId}"),
                    expectedSource,
                    externalId,
                    game.Title.Trim(),
                    NormalizeOptional(game.Platform),
                    game.PlaytimeSeconds is < 0 ? null : game.PlaytimeSeconds,
                    game.LastPlayedUtc?.ToUniversalTime(),
                    DateTimeOffset.UtcNow);

                await _history.UpsertAsync(record, cancellationToken);
                stored++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                warnings.Add($"{game.Title}: {ex.Message}");
            }
        }

        return new HistoryImportResult(
            expectedSource,
            games.Count,
            stored,
            warnings);
    }

    private static string NormalizeExternalId(HistoryImportGame game)
    {
        if (!string.IsNullOrWhiteSpace(game.ExternalId))
        {
            return game.ExternalId.Trim();
        }

        var normalized = GameTitleNormalizer.Normalize(game.Title);
        var platform = NormalizeOptional(game.Platform) ?? "unknown";
        return StableId.FromText($"history-fallback:{game.Source}:{normalized}:{platform}")
            .ToString("N");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
