namespace GameLauncher.Core.Models;

public sealed record LauncherUpdateInfo(
    Version CurrentVersion,
    Version LatestVersion,
    string ReleaseName,
    Uri ReleasePage,
    Uri? InstallerDownload,
    bool IsUpdateAvailable);
