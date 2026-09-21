namespace GameLauncher.Core.Models;

public sealed record HatchableSyncSettings(
    string BaseUrl,
    string Token,
    bool AutoSync)
{
    public const string DefaultBaseUrl = "https://my-game-library.hatchable.site";

    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(Token);

    public HatchableSyncSettings Normalize()
    {
        var url = (BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        return this with
        {
            BaseUrl = url,
            Token = (Token ?? string.Empty).Trim()
        };
    }
}
