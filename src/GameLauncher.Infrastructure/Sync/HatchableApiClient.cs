using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameLauncher.Core.Models;
using GameLauncher.Core.Sync;

namespace GameLauncher.Infrastructure.Sync;

public sealed class HatchableApiClient : IHatchableApiClient
{
    private readonly HttpClient _http;

    public HatchableApiClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<IReadOnlyList<HatchableRemoteGame>> GetGamesAsync(
        HatchableSyncSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings = settings.Normalize();
        using var request = CreateRequest(
            HttpMethod.Get,
            settings,
            "/api/launcher-sync");

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<SyncResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Hatchable returned an empty sync response.");

        return payload.Games
            .Select(ToModel)
            .OrderBy(x => x.RankScore)
            .ToArray();
    }

    public async Task<int> PushGamesAsync(
        HatchableSyncSettings settings,
        IReadOnlyList<HatchableGamePush> games,
        CancellationToken cancellationToken = default)
    {
        settings = settings.Normalize();

        var body = new
        {
            games = games.Select(x => new
            {
                remote_game_id = x.RemoteGameId,
                playtime_seconds = x.PlaytimeSeconds,
                last_played_at = x.LastPlayedAt?.ToUniversalTime().ToString("O"),
                progress_status = x.ProgressStatus,
                rating = x.Rating
            }).ToArray()
        };

        using var request = CreateRequest(
            HttpMethod.Post,
            settings,
            "/api/launcher-sync");
        request.Content = JsonContent.Create(body);

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<PushResponse>(
            JsonOptions,
            cancellationToken)
            ?? throw new InvalidOperationException("Hatchable returned an empty push response.");

        return payload.Updated;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        HatchableSyncSettings settings,
        string path)
    {
        var request = new HttpRequestMessage(
            method,
            settings.BaseUrl.TrimEnd('/') + path);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", settings.Token);
        request.Headers.UserAgent.ParseAdd("GameLauncher/5.0");
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        string? detail = null;
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken);
            if (json.TryGetProperty("error", out var error))
            {
                detail = error.GetString();
            }
        }
        catch
        {
            // Use HTTP status when response is not JSON.
        }

        throw new InvalidOperationException(
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Hatchable launcher token is invalid or revoked."
                : $"Hatchable sync failed ({(int)response.StatusCode}){(detail is null ? "." : $": {detail}")}");
    }

    private static HatchableRemoteGame ToModel(RemoteGameDto x) =>
        new(
            x.Id,
            x.Title ?? string.Empty,
            x.Platforms ?? string.Empty,
            x.SteamAppId,
            x.CoverUrl,
            x.RankScore,
            x.Status,
            x.ProgressStatus,
            x.Rating,
            x.PlaytimeSeconds,
            x.LastPlayedAt);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record SyncResponse(
        int Version,
        IReadOnlyList<RemoteGameDto> Games);

    private sealed record PushResponse(
        bool Ok,
        int Updated);

    private sealed record RemoteGameDto(
        int Id,
        string? Title,
        string? Platforms,
        [property: JsonPropertyName("steam_app_id")] long? SteamAppId,
        [property: JsonPropertyName("cover_url")] string? CoverUrl,
        [property: JsonPropertyName("rank_score")] int RankScore,
        string? Status,
        [property: JsonPropertyName("progress_status")] string? ProgressStatus,
        int? Rating,
        [property: JsonPropertyName("playtime_seconds")] long PlaytimeSeconds,
        [property: JsonPropertyName("last_played_at")] DateTimeOffset? LastPlayedAt);
}
