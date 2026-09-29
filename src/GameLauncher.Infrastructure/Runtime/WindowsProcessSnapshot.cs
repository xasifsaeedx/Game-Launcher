using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GameLauncher.Infrastructure.Runtime;

internal sealed record WindowsProcessEntry(
    int ProcessId,
    int ParentProcessId,
    string ExecutableName);

internal static class WindowsProcessSnapshot
{
    private const uint Th32csSnapProcess = 0x00000002;

    public static IReadOnlyDictionary<int, WindowsProcessEntry> Capture()
    {
        try
        {
            using var snapshot = CreateToolhelp32Snapshot(
                Th32csSnapProcess,
                0);

            if (snapshot.IsInvalid)
            {
                return CaptureFallback();
            }

            var result = new Dictionary<int, WindowsProcessEntry>();
            var native = new ProcessEntry32
            {
                DwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            if (!Process32FirstW(snapshot, ref native))
            {
                return result;
            }

            do
            {
                if (native.Th32ProcessId > 0 &&
                    native.Th32ProcessId <= int.MaxValue)
                {
                    var processId = (int)native.Th32ProcessId;
                    var parentProcessId = native.Th32ParentProcessId <= int.MaxValue
                        ? (int)native.Th32ParentProcessId
                        : 0;

                    result[processId] = new WindowsProcessEntry(
                        processId,
                        parentProcessId,
                        native.SzExeFile ?? string.Empty);
                }

                native.DwSize = (uint)Marshal.SizeOf<ProcessEntry32>();
            }
            while (Process32NextW(snapshot, ref native));

            return result;
        }
        catch
        {
            return CaptureFallback();
        }
    }

    public static bool IsAlive(
        int processId,
        string? expectedExecutableName = null)
    {
        var snapshot = Capture();
        if (!snapshot.TryGetValue(processId, out var entry))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(expectedExecutableName) ||
               string.Equals(
                   entry.ExecutableName,
                   expectedExecutableName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<int, WindowsProcessEntry> CaptureFallback()
    {
        var result = new Dictionary<int, WindowsProcessEntry>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var name = string.Empty;
                try
                {
                    name = process.ProcessName + ".exe";
                }
                catch
                {
                    // PID-only fallback is still useful for liveness checks.
                }

                result[process.Id] = new WindowsProcessEntry(
                    process.Id,
                    0,
                    name);
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint DwSize;
        public uint CntUsage;
        public uint Th32ProcessId;
        public IntPtr Th32DefaultHeapId;
        public uint Th32ModuleId;
        public uint CntThreads;
        public uint Th32ParentProcessId;
        public int PcPriClassBase;
        public uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string SzExeFile;
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(
        uint dwFlags,
        uint th32ProcessId);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(
        SafeFileHandle hSnapshot,
        ref ProcessEntry32 lppe);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(
        SafeFileHandle hSnapshot,
        ref ProcessEntry32 lppe);
}
