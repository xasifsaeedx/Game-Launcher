using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Adapters;

internal static class GameDiscoveryUtilities
{
    private static readonly string[] ArtworkNames =
    [
        "cover.jpg", "cover.jpeg", "cover.png",
        "poster.jpg", "poster.jpeg", "poster.png",
        "library_600x900.jpg", "library_600x900.png",
        "background.jpg", "background.png"
    ];

    private static readonly string[] ExecutableExclusions =
    [
        "launcher", "unins", "setup", "update", "updater", "crash", "report",
        "redistributable", "redist", "benchmark", "config", "support", "helper"
    ];

    public static string? FindLocalArtwork(string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath)) return null;

        foreach (var name in ArtworkNames)
        {
            var direct = Path.Combine(installPath, name);
            if (File.Exists(direct)) return direct;
        }

        foreach (var subfolder in new[] { "art", "artwork", "images", "resources", "support" })
        {
            var folder = Path.Combine(installPath, subfolder);
            if (!Directory.Exists(folder)) continue;

            foreach (var name in ArtworkNames)
            {
                var path = Path.Combine(folder, name);
                if (File.Exists(path)) return path;
            }
        }

        return null;
    }

    public static string? FindBestExecutable(string? installPath, string title)
    {
        if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath)) return null;

        var titleTokens = GameTitleNormalizer.Normalize(title)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length >= 3)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<(string Path, int Score)>();
        foreach (var file in EnumerateExecutables(installPath))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (ExecutableExclusions.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase))) continue;

            var normalized = GameTitleNormalizer.Normalize(name);
            var score = 1;
            foreach (var token in titleTokens)
            {
                if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase)) score += 10;
            }

            try
            {
                score += (int)Math.Min(20, new FileInfo(file).Length / (50L * 1024 * 1024));
            }
            catch
            {
                // File size is only a tie-breaker.
            }

            candidates.Add((file, score));
        }

        return candidates
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Path.Length)
            .Select(x => x.Path)
            .FirstOrDefault();
    }

    public static string? CleanExecutablePath(string? value, string? installPath = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var path = value.Trim().Trim('"');
        var comma = path.LastIndexOf(',');
        if (comma > 2 && int.TryParse(path[(comma + 1)..], out _))
        {
            path = path[..comma].Trim().Trim('"');
        }

        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(installPath))
        {
            path = Path.Combine(installPath, path);
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsLikelyGameTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;

        var excluded = new[]
        {
            "launcher", "client", "anti-cheat", "anticheat", "updater", "update service",
            "redistributable", "visual c++", "directx", "crash reporter", "battle.net",
            "ubisoft connect", "ea app", "origin"
        };

        return !excluded.Any(x => title.Contains(x, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateExecutables(string installPath)
    {
        IEnumerable<string> direct;
        try
        {
            direct = Directory.EnumerateFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (var file in direct) yield return file;

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(installPath).Take(12).ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (var directory in directories)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                    .Take(30)
                    .ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var file in files) yield return file;
        }
    }
}
