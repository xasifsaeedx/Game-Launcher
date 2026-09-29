using GameLauncher.Core.Models;

namespace GameLauncher.Infrastructure.Runtime;

internal static class WindowsGameProcessPolicy
{
    private static readonly string[] HelperProcessTerms =
    [
        "launcher",
        "crash",
        "report",
        "updater",
        "update",
        "redist",
        "setup",
        "unins",
        "anticheat",
        "anti-cheat",
        "easyanticheat",
        "battleye",
        "beservice",
        "beclient",
        "bootstrap",
        "start_protected_game"
    ];

    public static bool ShouldTrackDirectProcess(
        GameInstallation installation,
        string? executableName,
        string? accessiblePath,
        bool isRunning)
    {
        ArgumentNullException.ThrowIfNull(installation);

        if (!isRunning ||
            installation.Source == GameSource.Xbox ||
            IsHelperExecutable(executableName))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(accessiblePath) &&
            !string.IsNullOrWhiteSpace(installation.ExecutablePath) &&
            PathsEqual(accessiblePath, installation.ExecutablePath))
        {
            return true;
        }

        return MatchesConfiguredExecutableName(
            installation,
            executableName);
    }

    public static bool IsCandidate(
        GameInstallation installation,
        WindowsProcessEntry process,
        bool isDescendant,
        bool wasPresentBefore,
        string? accessiblePath)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(process);

        if (IsHelperExecutable(process.ExecutableName))
        {
            return false;
        }

        if (MatchesConfiguredExecutableName(
                installation,
                process.ExecutableName) &&
            (!wasPresentBefore || isDescendant))
        {
            return true;
        }

        if (isDescendant)
        {
            return true;
        }

        if (wasPresentBefore ||
            string.IsNullOrWhiteSpace(accessiblePath))
        {
            return false;
        }

        return IsConfiguredExecutablePath(
                   installation,
                   accessiblePath) ||
               IsUnderInstallDirectory(
                   installation,
                   accessiblePath);
    }

    public static int ScoreCandidate(
        GameInstallation installation,
        WindowsProcessEntry process,
        bool isDescendant,
        bool wasPresentBefore,
        string? accessiblePath,
        long workingSetBytes)
    {
        if (IsHelperExecutable(process.ExecutableName))
        {
            return int.MinValue;
        }

        var score = 0;

        if (IsConfiguredExecutablePath(
                installation,
                accessiblePath))
        {
            score += 500;
        }

        if (MatchesConfiguredExecutableName(
                installation,
                process.ExecutableName))
        {
            score += 400;
        }

        if (isDescendant)
        {
            score += 300;
        }

        if (IsUnderInstallDirectory(
                installation,
                accessiblePath))
        {
            score += 200;
        }

        if (!wasPresentBefore)
        {
            score += 100;
        }

        if (workingSetBytes > 0)
        {
            score += (int)Math.Min(
                100,
                workingSetBytes / (10 * 1024 * 1024));
        }

        return score;
    }

    public static HashSet<int> ExpandLineage(
        IEnumerable<WindowsProcessEntry> processes,
        IEnumerable<int> rootProcessIds)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(rootProcessIds);

        var entries = processes.ToArray();
        var lineage = rootProcessIds
            .Where(x => x > 0)
            .ToHashSet();

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var process in entries)
            {
                if (lineage.Contains(process.ProcessId) ||
                    !lineage.Contains(process.ParentProcessId))
                {
                    continue;
                }

                lineage.Add(process.ProcessId);
                changed = true;
            }
        }

        return lineage;
    }

    public static bool MatchesConfiguredExecutableName(
        GameInstallation installation,
        string? executableName)
    {
        if (string.IsNullOrWhiteSpace(installation.ExecutablePath) ||
            string.IsNullOrWhiteSpace(executableName))
        {
            return false;
        }

        var expected = Path.GetFileName(
            installation.ExecutablePath);

        return string.Equals(
            expected,
            Path.GetFileName(executableName),
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHelperExecutable(
        string? executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            return false;
        }

        var normalized = Path
            .GetFileNameWithoutExtension(executableName)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();

        return HelperProcessTerms.Any(term =>
            normalized.Contains(
                term.Replace(" ", string.Empty),
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsConfiguredExecutablePath(
        GameInstallation installation,
        string? candidatePath)
    {
        return !string.IsNullOrWhiteSpace(candidatePath) &&
               !string.IsNullOrWhiteSpace(installation.ExecutablePath) &&
               PathsEqual(
                   candidatePath,
                   installation.ExecutablePath);
    }

    private static bool IsUnderInstallDirectory(
        GameInstallation installation,
        string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) ||
            string.IsNullOrWhiteSpace(installation.InstallPath))
        {
            return false;
        }

        try
        {
            var root = Path
                .GetFullPath(installation.InstallPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            var fullCandidate = Path.GetFullPath(candidatePath);

            return fullCandidate.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool PathsEqual(
        string left,
        string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
