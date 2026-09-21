using System.Text.RegularExpressions;

namespace GameLauncher.Infrastructure.Adapters;

public static class SteamLibraryParser
{
    private static readonly Regex PathRegex = new(
        @"""path""\s*""(?<path>(?:\\.|[^""])*)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<string> ParseLibraryPaths(string text, string steamRoot)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddIfValid(paths, steamRoot);

        if (string.IsNullOrWhiteSpace(text)) return paths.ToArray();

        foreach (Match match in PathRegex.Matches(text))
        {
            var rawPath = match.Groups["path"].Value.Replace("\\\\", "\\", StringComparison.Ordinal);
            AddIfValid(paths, rawPath);
        }

        return paths.ToArray();
    }

    private static void AddIfValid(HashSet<string> paths, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            paths.Add(Path.GetFullPath(path.Trim()));
        }
        catch
        {
            // Ignore malformed paths from a damaged VDF file.
        }
    }
}
