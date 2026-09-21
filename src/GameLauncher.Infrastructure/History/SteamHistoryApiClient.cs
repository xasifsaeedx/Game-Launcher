using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GameLauncher.Core.History;
using GameLauncher.Core.Models;

namespace GameLauncher.Infrastructure.History;

public sealed class SteamHistoryApiClient : ISteamHistoryClient
{
    private readonly HttpClient _http;

    public SteamHistoryApiClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<IReadOnlyList<HistoryImportGame>> GetOwnedGamesAsync(
        SteamHistorySettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();

        if (!settings.IsConfigured)
        {
            throw new ArgumentException(
                "SteamID64 and Steam Web API key are required.",
                nameof(settings));
        }

        var url =
            "https://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/" +
            $"?key={Uri.EscapeDataString(settings.ApiKey)}" +
            $"&steamid={Uri.EscapeDataString(settings.SteamId64)}" +
            "&include_appinfo=true" +
            "&include_played_free_games=true" +
            "&format=json";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("GameLauncher/7.0");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Steam history request failed with HTTP {(int)response.StatusCode}.");
        }

        var payload = await response.Content.ReadFromJsonAsync<SteamEnvelope>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Steam returned an empty response.");

        var games = payload.Response?.Games ?? Array.Empty<SteamOwnedGame>();

        return games
            .Where(x => x.AppId > 0 && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new HistoryImportGame(
                GameSource.Steam,
                x.AppId.ToString(),
                x.Name!.Trim(),
                "PC",
                x.PlaytimeForeverMinutes.HasValue
                    ? checked((long)x.PlaytimeForeverMinutes.Value * 60L)
                    : null,
                x.LastPlayedUnixSeconds is > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(x.LastPlayedUnixSeconds.Value)
                    : null))
            .ToArray();
    }

    private sealed record SteamEnvelope(
        [property: JsonPropertyName("response")] SteamResponse? Response);

    private sealed record SteamResponse(
        [property: JsonPropertyName("game_count")] int GameCount,
        [property: JsonPropertyName("games")] IReadOnlyList<SteamOwnedGame>? Games);

    private sealed record SteamOwnedGame(
        [property: JsonPropertyName("appid")] long AppId,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("playtime_forever")] long? PlaytimeForeverMinutes,
        [property: JsonPropertyName("rtime_last_played")] long? LastPlayedUnixSeconds);
}
