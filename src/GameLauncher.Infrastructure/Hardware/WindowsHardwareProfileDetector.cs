using System.Runtime.InteropServices;
using GameLauncher.Core.Hardware;
using GameLauncher.Core.Models;
using GameLauncher.Core.Services;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;

namespace GameLauncher.Infrastructure.Hardware;

public sealed class WindowsHardwareProfileDetector : IHardwareProfileDetector
{
    public Task<HardwareProfile> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cpu = DetectCpuName();
        var gpu = DetectGpuName();
        var ram = DetectRamGb();
        var (width, height, refresh) = DetectDisplay();

        return Task.FromResult(
            new HardwareProfile(
                cpu,
                gpu,
                ram,
                width,
                height,
                refresh,
                HardwareTierClassifier.ClassifyGpuName(gpu),
                DateTimeOffset.UtcNow));
    }

    private static string DetectCpuName()
    {
        if (!OperatingSystem.IsWindows()) return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return Convert.ToString(key?.GetValue("ProcessorNameString"))?.Trim()
                ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")
                ?? "Unknown CPU";
        }
        catch
        {
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown CPU";
        }
    }

    private static string DetectGpuName()
    {
        var computer = new Computer { IsGpuEnabled = true };
        try
        {
            computer.Open();

            var candidates = computer.Hardware
                .Where(x => x.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                .OrderBy(x => x.HardwareType == HardwareType.GpuIntel ? 1 : 0)
                .ToArray();

            return candidates.FirstOrDefault()?.Name?.Trim() ?? "Unknown GPU";
        }
        catch
        {
            return "Unknown GPU";
        }
        finally
        {
            computer.Close();
        }
    }

    private static double DetectRamGb()
    {
        if (!OperatingSystem.IsWindows()) return 0;

        try
        {
            var status = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(status)) return 0;
            return status.TotalPhysical / 1024d / 1024d / 1024d;
        }
        catch
        {
            return 0;
        }
    }

    private static (int Width, int Height, int Refresh) DetectDisplay()
    {
        if (!OperatingSystem.IsWindows()) return (0, 0, 0);

        var dc = GetDC(IntPtr.Zero);
        if (dc == IntPtr.Zero) return (0, 0, 0);

        try
        {
            return (
                GetDeviceCaps(dc, 8),
                GetDeviceCaps(dc, 10),
                GetDeviceCaps(dc, 116));
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int index);
}
