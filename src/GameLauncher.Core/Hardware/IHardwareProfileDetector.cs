using GameLauncher.Core.Models;

namespace GameLauncher.Core.Hardware;

public interface IHardwareProfileDetector
{
    Task<HardwareProfile> DetectAsync(
        CancellationToken cancellationToken = default);
}
