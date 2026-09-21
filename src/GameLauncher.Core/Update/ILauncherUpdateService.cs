using GameLauncher.Core.Models;

namespace GameLauncher.Core.Update;

public interface ILauncherUpdateService
{
    Task<LauncherUpdateInfo> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default);

    Task<string> DownloadInstallerAsync(
        LauncherUpdateInfo update,
        CancellationToken cancellationToken = default);
}
