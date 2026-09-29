namespace GameLauncher.Tests;

internal static class WindowsProcessTrackingTests
{
    internal static Task ElevatedDirectProcessDoesNotRequirePathAccess()
    {
        var installation = CreateInstallation(
            @"C:\Games\ACS\ACShadows.exe");

        var shouldTrack = WindowsGameProcessPolicy.ShouldTrackDirectProcess(
            installation,
            "ACShadows.exe",
            accessiblePath: null,
            isRunning: true);

        Assert.True(shouldTrack);
        return Task.CompletedTask;
    }

    internal static Task PreExistingSameNameProcessIsRejected()
    {
        var installation = CreateInstallation(
            @"C:\Games\ACS\ACShadows.exe");

        var process = new WindowsProcessEntry(
            2200,
            1,
            "ACShadows.exe");

        var candidate = WindowsGameProcessPolicy.IsCandidate(
            installation,
            process,
            isDescendant: false,
            wasPresentBefore: true,
            accessiblePath: null);

        Assert.True(!candidate);
        return Task.CompletedTask;
    }

    internal static Task NewSameNameElevatedProcessIsAcceptedWithoutPath()
    {
        var installation = CreateInstallation(
            @"C:\Games\ACS\ACShadows.exe");

        var process = new WindowsProcessEntry(
            2201,
            1,
            "ACShadows.exe");

        var candidate = WindowsGameProcessPolicy.IsCandidate(
            installation,
            process,
            isDescendant: false,
            wasPresentBefore: false,
            accessiblePath: null);

        Assert.True(candidate);
        return Task.CompletedTask;
    }

    internal static Task ProcessTreeExpansionFindsLauncherHandoff()
    {
        WindowsProcessEntry[] processes =
        [
            new(3001, 3000, "bootstrap.exe"),
            new(3002, 3001, "anticheat.exe"),
            new(3003, 3002, "RealGame.exe"),
            new(4000, 1, "Unrelated.exe")
        ];

        var lineage = WindowsGameProcessPolicy.ExpandLineage(
            processes,
            new[] { 3000 });

        Assert.True(lineage.Contains(3000));
        Assert.True(lineage.Contains(3001));
        Assert.True(lineage.Contains(3002));
        Assert.True(lineage.Contains(3003));
        Assert.True(!lineage.Contains(4000));
        return Task.CompletedTask;
    }

    internal static Task HelperProcessIsRejectedDuringHandoff()
    {
        var installation = CreateInstallation(
            @"C:\Games\Example\RealGame.exe");

        var helper = new WindowsProcessEntry(
            5001,
            5000,
            "EasyAntiCheat_EOS.exe");

        var game = new WindowsProcessEntry(
            5002,
            5001,
            "RealGame.exe");

        Assert.True(!WindowsGameProcessPolicy.IsCandidate(
            installation,
            helper,
            isDescendant: true,
            wasPresentBefore: false,
            accessiblePath: null));

        Assert.True(WindowsGameProcessPolicy.IsCandidate(
            installation,
            game,
            isDescendant: true,
            wasPresentBefore: false,
            accessiblePath: null));

        return Task.CompletedTask;
    }

    private static GameInstallation CreateInstallation(
        string executablePath) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            GameSource.Manual,
            Guid.NewGuid().ToString("D"),
            Path.GetDirectoryName(executablePath),
            executablePath,
            null,
            true);
}
