namespace GameLauncher.Core.Models;

public sealed record PersonalLibrarySettings(
    string SheetUrl,
    bool AutoSync)
{
    public PersonalLibrarySettings Normalize() =>
        this with { SheetUrl = (SheetUrl ?? string.Empty).Trim() };

    public bool IsConfigured
    {
        get
        {
            if (!Uri.TryCreate(SheetUrl, UriKind.Absolute, out var uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttps &&
                   string.Equals(uri.Host, "docs.google.com", StringComparison.OrdinalIgnoreCase);
        }
    }
}
