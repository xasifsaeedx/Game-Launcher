using System.Diagnostics;
using GameLauncher.Core.Models;
using GameLauncher.Core.Runtime;

namespace GameLauncher.Infrastructure.Runtime;

public sealed class WindowsGameRuntime : IGameRuntime
{
    private static readonly TimeSpan DetectionTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DirectProcessStabilityDelay = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan CandidateStabilityDelay = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);

    public async Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);

        var before = WindowsProcessSnapshot.Capture();
        var startedUtc = DateTimeOffset.UtcNow;

        var directProcess = StartInstallation(installation);
        var launchRootIds = new HashSet<int>();

        if (directProcess is not null)
        {
            var directId = TryGetProcessId(directProcess);
            if (directId.HasValue)
            {
                launchRootIds.Add(directId.Value);
            }

            if (await CanTrackDirectProcessAsync(
                    directProcess,
                    installation,
                    cancellationToken))
            {
                var detectedPath =
                    TryGetProcessPath(directProcess) ??
                    installation.ExecutablePath;

                return new WindowsGameRunHandle(
                    directProcess,
                    startedUtc,
                    detectedPath);
            }

            directProcess.Dispose();
        }

        var detected = await WaitForGameProcessAsync(
            installation,
            before,
            launchRootIds,
            DetectionTimeout,
            cancellationToken);

        if (detected is null)
        {
            throw new InvalidOperationException(
                "The game was launched, but its running process could not be detected within 60 seconds.");
        }

        return new WindowsGameRunHandle(
            detected,
            startedUtc,
            TryGetProcessPath(detected));
    }

    private static Process? StartInstallation(
        GameInstallation installation)
    {
        if (!string.IsNullOrWhiteSpace(installation.LaunchUri))
        {
            return Process.Start(
                new ProcessStartInfo(installation.LaunchUri)
                {
                    UseShellExecute = true
                });
        }

        if (string.IsNullOrWhiteSpace(installation.ExecutablePath))
        {
            throw new InvalidOperationException(
                "This game does not have a launch URI or executable path.");
        }

        if (!File.Exists(installation.ExecutablePath))
        {
            throw new FileNotFoundException(
                "The configured game executable no longer exists.",
                installation.ExecutablePath);
        }

        return Process.Start(
            new ProcessStartInfo(installation.ExecutablePath)
            {
                UseShellExecute = true,
                WorkingDirectory =
                    !string.IsNullOrWhiteSpace(installation.InstallPath)
                        ? installation.InstallPath
                        : Path.GetDirectoryName(
                              installation.ExecutablePath) ??
                          string.Empty,
                Arguments =
                    installation.LaunchArguments ??
                    string.Empty
            });
    }

    private static async Task<bool> CanTrackDirectProcessAsync(
        Process process,
        GameInstallation installation,
        CancellationToken cancellationToken)
    {
        var processId = TryGetProcessId(process);
        if (!processId.HasValue)
        {
            return false;
        }

        var executableName = TryGetExecutableName(process);
        var accessiblePath = TryGetProcessPath(process);

        if (!WindowsGameProcessPolicy.ShouldTrackDirectProcess(
                installation,
                executableName,
                accessiblePath,
                IsStillRunning(process)))
        {
            return false;
        }

        await Task.Delay(
            DirectProcessStabilityDelay,
            cancellationToken);

        return WindowsProcessSnapshot.IsAlive(
            processId.Value,
            executableName);
    }

    private static async Task<Process?> WaitForGameProcessAsync(
        GameInstallation installation,
        IReadOnlyDictionary<int, WindowsProcessEntry> before,
        IReadOnlyCollection<int> launchRootIds,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshot = WindowsProcessSnapshot.Capture();
            var lineage = WindowsGameProcessPolicy.ExpandLineage(
                snapshot.Values,
                launchRootIds);

            var candidates = FindCandidates(
                    installation,
                    before,
                    snapshot,
                    lineage)
                .OrderByDescending(x => x.Score)
                .ToArray();

            if (candidates.Length > 0)
            {
                await Task.Delay(
                    CandidateStabilityDelay,
                    cancellationToken);

                foreach (var candidate in candidates)
                {
                    if (!WindowsProcessSnapshot.IsAlive(
                            candidate.Entry.ProcessId,
                            candidate.Entry.ExecutableName))
                    {
                        continue;
                    }

                    var process = TryOpenProcess(
                        candidate.Entry.ProcessId);

                    if (process is not null)
                    {
                        return process;
                    }
                }
            }

            await Task.Delay(
                PollInterval,
                cancellationToken);
        }

        return null;
    }

    private static IEnumerable<ProcessCandidate> FindCandidates(
        GameInstallation installation,
        IReadOnlyDictionary<int, WindowsProcessEntry> before,
        IReadOnlyDictionary<int, WindowsProcessEntry> current,
        IReadOnlySet<int> lineage)
    {
        foreach (var entry in current.Values)
        {
            if (entry.ProcessId <= 0)
            {
                continue;
            }

            var wasPresentBefore = before.ContainsKey(
                entry.ProcessId);

            var isDescendant =
                lineage.Contains(entry.ProcessId) &&
                !wasPresentBefore;

            var accessiblePath = TryGetProcessPath(
                entry.ProcessId);

            if (!WindowsGameProcessPolicy.IsCandidate(
                    installation,
                    entry,
                    isDescendant,
                    wasPresentBefore,
                    accessiblePath))
            {
                continue;
            }

            var score = WindowsGameProcessPolicy.ScoreCandidate(
                installation,
                entry,
                isDescendant,
                wasPresentBefore,
                accessiblePath,
                TryGetWorkingSet(entry.ProcessId));

            yield return new ProcessCandidate(
                entry,
                score);
        }
    }

    private static int? TryGetProcessId(
        Process process)
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetExecutableName(
        Process process)
    {
        try
        {
            return process.ProcessName + ".exe";
        }
        catch
        {
            return null;
        }
    }

    private static Process? TryOpenProcess(
        int processId)
    {
        try
        {
            var process = Process.GetProcessById(
                processId);

            if (process.HasExited)
            {
                process.Dispose();
                return null;
            }

            return process;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsStillRunning(
        Process process)
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

    private static string? TryGetProcessPath(
        Process process)
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

    private static string? TryGetProcessPath(
        int processId)
    {
        using var process = TryOpenProcess(
            processId);

        return process is null
            ? null
            : TryGetProcessPath(process);
    }

    private static long TryGetWorkingSet(
        int processId)
    {
        using var process = TryOpenProcess(
            processId);

        if (process is null)
        {
            return 0;
        }

        try
        {
            return process.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }

    private sealed record ProcessCandidate(
        WindowsProcessEntry Entry,
        int Score);
}
