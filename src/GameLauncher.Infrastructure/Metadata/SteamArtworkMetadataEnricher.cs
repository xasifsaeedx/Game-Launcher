using System.Net;
using System.Text.Json;
using GameLauncher.Core.Metadata;
using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Metadata;

public sealed class SteamArtworkMetadataEnricher : IGameMetadataEnricher
{
    private readonly string _coverDirectory;
    private readonly HttpClient _httpClient;

    public SteamArtworkMetadataEnricher(string coverDirectory, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coverDirectory);
        _coverDirectory = Path.GetFullPath(coverDirectory);
        Directory.CreateDirectory(_coverDirectory);

        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GameLauncher/2.0");
        }
    }

    public async Task<Game> EnrichAsync(
        Game game,
        IReadOnlyList<GameInstallation> installations,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(game.CoverImagePath) &&
            File.Exists(game.CoverImagePath))
        {
            return game;
        }

        var appId = installations
            .Where(x => x.Source == GameSource.Steam)
            .Select(x => x.ExternalId)
            .FirstOrDefault(IsNumeric);

        appId ??= await FindExactSteamAppIdAsync(game.Title, cancellationToken);
        if (appId is null) return game;

        var cacheName = $"{StableId.FromText($"cover:{GameTitleNormalizer.Normalize(game.Title)}"):N}.jpg";
        var cachePath = Path.Combine(_coverDirectory, cacheName);

        if (!File.Exists(cachePath))
        {
            var downloaded = await TryDownloadArtworkAsync(appId, cachePath, cancellationToken);
            if (!downloaded) return game;
        }

        return game with
        {
            CoverImagePath = cachePath,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task<string?> FindExactSteamAppIdAsync(
        string title,
        CancellationToken cancellationToken)
    {
        var normalizedTitle = GameTitleNormalizer.Normalize(title);
        if (normalizedTitle.Length < 2) return null;

        var uri =
            "https://store.steampowered.com/api/storesearch/" +
            $"?term={Uri.EscapeDataString(title)}&l=english&cc=CA";

        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("name", out var nameValue) ||
                nameValue.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("id", out var idValue))
            {
                continue;
            }

            var candidateName = nameValue.GetString();
            if (!string.Equals(
                    GameTitleNormalizer.Normalize(candidateName ?? string.Empty),
                    normalizedTitle,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (idValue.ValueKind == JsonValueKind.Number &&
                idValue.TryGetInt64(out var numericId))
            {
                return numericId.ToString();
            }

            if (idValue.ValueKind == JsonValueKind.String &&
                IsNumeric(idValue.GetString()))
            {
                return idValue.GetString();
            }
        }

        return null;
    }

    private async Task<bool> TryDownloadArtworkAsync(
        string appId,
        string destination,
        CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900_2x.jpg",
            $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg"
        };

        foreach (var uri in candidates)
        {
            using var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound) continue;
            if (!response.IsSuccessStatusCode) continue;

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length < 1024) continue;

            await File.WriteAllBytesAsync(destination, bytes, cancellationToken);
            return true;
        }

        return false;
    }

    private static bool IsNumeric(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit);
}
