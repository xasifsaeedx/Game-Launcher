using GameLauncher.Core.Models;

namespace GameLauncher.Core.Runtime;

public interface IGameRuntime
{
    Task<IGameRunHandle> LaunchAsync(
        GameInstallation installation,
        CancellationToken cancellationToken = default);
}
