using LibreHardwareMonitor.Hardware;

namespace GameLauncher.Infrastructure.Overlay;

internal sealed class GpuMetricsReader : IDisposable
{
    private readonly Computer _computer;

    public GpuMetricsReader()
    {
        _computer = new Computer
        {
            IsGpuEnabled = true
        };
        _computer.Open();
    }

    public (double? UsagePercent, double? TemperatureCelsius) Read()
    {
        IHardware? selectedGpu = null;

        foreach (var hardware in _computer.Hardware)
        {
            if (hardware.HardwareType is not (
                    HardwareType.GpuNvidia or
                    HardwareType.GpuAmd or
                    HardwareType.GpuIntel))
            {
                continue;
            }

            hardware.Update();
            selectedGpu = hardware;

            if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
            {
                break;
            }
        }

        if (selectedGpu is null) return (null, null);

        var usage = selectedGpu.Sensors
            .Where(x => x.SensorType == SensorType.Load && x.Value.HasValue)
            .OrderByDescending(x =>
                x.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("D3D", StringComparison.OrdinalIgnoreCase))
            .Select(x => (double?)x.Value!.Value)
            .FirstOrDefault();

        var temperature = selectedGpu.Sensors
            .Where(x => x.SensorType == SensorType.Temperature && x.Value.HasValue)
            .OrderByDescending(x => x.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            .Select(x => (double?)x.Value!.Value)
            .FirstOrDefault();

        return (usage, temperature);
    }

    public void Dispose()
    {
        _computer.Close();
    }
}
