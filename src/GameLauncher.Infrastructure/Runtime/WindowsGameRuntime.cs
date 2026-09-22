using System.Diagnostics;
using GameLauncher.Core.Models;
using GameLauncher.Core.Runtime;

namespace GameLauncher.Infrastructure.Runtime;

public sealed class WindowsGameRuntime : IGameRuntime
{
    private static readonly string[] HelperProcessTerms =
    [
        "launcher", "crash", "report", "updater", "update", "redist", "setup", "unins"
    ];

    public async Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);

        var before = SnapshotProcessIds();
        var startedUtc = DateTimeOffset.UtcNow;

        var directProcess = StartInstallation(installation);
        if (directProcess is not null &&
            !IsHelperProcess(directProcess) &&
            IsUsableDirectProcess(directProcess, installation))
        {
            var directPath = TryGetProcessPath(directProcess) ?? installation.ExecutablePath;
            return new WindowsGameRunHandle(directProcess, startedUtc, directPath);
        }

        directProcess?.Dispose();

        var detected = await WaitForGameProcessAsync(
            installation,
            before,
            startedUtc,
            TimeSpan.FromSeconds(60),
            cancellationToken);

        if (detected is null)
        {
            throw new InvalidOperationException(
                "The game was launched, but its running process could not be detected within 60 seconds.");
        }

        var detectedPath = TryGetProcessPath(detected);
        return new WindowsGameRunHandle(detected, startedUtc, detectedPath);
    }

    private static HashSet<int> SnapshotProcessIds()
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                ids.Add(process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        return ids;
    }

    private static Process? StartInstallation(GameInstallation installation)
    {
        if (!string.IsNullOrWhiteSpace(installation.LaunchUri))
        {
            return Process.Start(new ProcessStartInfo(installation.LaunchUri)
            {
                UseShellExecute = true
            });
        }

        if (string.IsNullOrWhiteSpace(installation.ExecutablePath))
        {
            throw new InvalidOperationException("This game does not have a launch URI or executable path.");
        }

        if (!File.Exists(installation.ExecutablePath))
        {
            throw new FileNotFoundException("The configured game executable no longer exists.", installation.ExecutablePath);
        }

        var startInfo = new ProcessStartInfo(installation.ExecutablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = !string.IsNullOrWhiteSpace(installation.InstallPath)
                ? installation.InstallPath
                : Path.GetDirectoryName(installation.ExecutablePath) ?? string.Empty,
            Arguments = installation.LaunchArguments ?? string.Empty
        };

        return Process.Start(startInfo);
    }

    private static bool IsHelperProcess(Process process)
    {
        try
        {
            return HelperProcessTerms.Any(term =>
                process.ProcessName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return true;
        }
    }

    private static bool IsUsableDirectProcess(Process process, GameInstallation installation)
    {
        try
        {
            if (process.HasExited) return false;

            if (string.IsNullOrWhiteSpace(installation.ExecutablePath))
            {
                return false;
            }

            var path = TryGetProcessPath(process);
            return !string.IsNullOrWhiteSpace(path) &&
                   PathsEqual(path, installation.ExecutablePath);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<Process?> WaitForGameProcessAsync(
        GameInstallation installation,
        HashSet<int> before,
        DateTimeOffset launchedUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidates = FindCandidates(installation, before, launchedUtc).ToArray();
            if (candidates.Length > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                var surviving = candidates
                    .Where(IsStillRunning)
                    .OrderByDescending(ScoreCandidate)
                    .ToArray();

                if (surviving.Length > 0)
                {
                    var selected = surviving[0];
                    foreach (var candidate in candidates)
                    {
                        if (candidate.Id != selected.Id) candidate.Dispose();
                    }

                    return selected;
                }

                foreach (var candidate in candidates) candidate.Dispose();
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        return null;
    }

    private static IEnumerable<Process> FindCandidates(
        GameInstallation installation,
        HashSet<int> before,
        DateTimeOffset launchedUtc)
    {
        foreach (var process in Process.GetProcesses())
        {
            var keep = false;
            try
            {
                if (process.HasExited) continue;

                var path = TryGetProcessPath(process);
                if (string.IsNullOrWhiteSpace(path)) continue;

                if (!string.IsNullOrWhiteSpace(installation.ExecutablePath) &&
                    PathsEqual(path, installation.ExecutablePath) &&
                    !IsHelperProcess(process))
                {
                    keep = true;
                }
                else if (!string.IsNullOrWhiteSpace(installation.InstallPath) &&
                         IsPathUnderDirectory(path, installation.InstallPath) &&
                         (!before.Contains(process.Id) || WasStartedNearLaunch(process, launchedUtc)))
                {
                    keep = true;
                }
            }
            catch
            {
                keep = false;
            }

            if (keep)
            {
                yield return process;
            }
            else
            {
                process.Dispose();
            }
        }
    }

    private static bool WasStartedNearLaunch(Process process, DateTimeOffset launchedUtc)
    {
        try
        {
            return process.StartTime.ToUniversalTime() >= launchedUtc.UtcDateTime.AddSeconds(-5);
        }
        catch
        {
            return false;
        }
    }

    private static int ScoreCandidate(Process process)
    {
        var score = 100;
        try
        {
            var name = process.ProcessName;
            if (HelperProcessTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            {
                score -= 80;
            }

            score += (int)Math.Min(100, process.WorkingSet64 / (1024 * 1024 * 10));
        }
        catch
        {
            // Keep the neutral score.
        }

        return score;
    }

    private static bool IsStillRunning(Process process)
    {
        try
        {
            process.Refresh();
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static bool PathsEqual(string left, string right)
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

    private static bool IsPathUnderDirectory(string candidate, string directory)
    {
        try
        {
            var root = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var fullCandidate = Path.GetFullPath(candidate);
            return fullCandidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
