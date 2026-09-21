using GameLauncher.Core.Models;

namespace GameLauncher.Core.Runtime;

public interface IExternalProgramRuntime
{
    Task<IExternalProgramHandle> StartAsync(
        LaunchAction action,
        CancellationToken cancellationToken = default);
}
