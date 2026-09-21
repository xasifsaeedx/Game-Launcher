using System.Text.RegularExpressions;

namespace GameLauncher.Infrastructure.Adapters;

public sealed record SteamManifest(string AppId, string Name, string InstallDirectoryName);

public static class SteamManifestParser
{
    private static readonly Regex KeyValueRegex = new(
        @"^\s*""(?<key>[^""]+)""\s*""(?<value>(?:\\.|[^""])*)""\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    public static SteamManifest? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in KeyValueRegex.Matches(text))
        {
            values[match.Groups["key"].Value] = Unescape(match.Groups["value"].Value);
        }

        if (!values.TryGetValue("appid", out var appId) ||
            !values.TryGetValue("name", out var name) ||
            !values.TryGetValue("installdir", out var installDirectoryName) ||
            string.IsNullOrWhiteSpace(appId) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(installDirectoryName))
        {
            return null;
        }

        return new SteamManifest(appId.Trim(), name.Trim(), installDirectoryName.Trim());
    }

    private static string Unescape(string value) =>
        value.Replace("\\\\", "\\", StringComparison.Ordinal)
             .Replace("\\\"", "\"", StringComparison.Ordinal);
}
