namespace GameLauncher.Core.Models;

public sealed record SteamHistorySettings(
    string SteamId64,
    string ApiKey,
    bool AutoSync)
{
    public SteamHistorySettings Normalize() =>
        this with
        {
            SteamId64 = (SteamId64 ?? string.Empty).Trim(),
            ApiKey = (ApiKey ?? string.Empty).Trim()
        };

    public bool IsConfigured =>
        SteamId64.Length is >= 16 and <= 20 &&
        SteamId64.All(char.IsDigit) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
