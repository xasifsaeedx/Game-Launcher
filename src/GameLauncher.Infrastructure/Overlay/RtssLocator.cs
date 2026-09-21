using Microsoft.Win32;

namespace GameLauncher.Infrastructure.Overlay;

internal static class RtssLocator
{
    public static string? FindExecutable()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;

                foreach (var subKeyName in uninstall.GetSubKeyNames())
                {
                    using var key = uninstall.OpenSubKey(subKeyName);
                    var displayName = key?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(displayName) ||
                        !displayName.Contains("RivaTuner Statistics Server", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var installLocation = key?.GetValue("InstallLocation") as string;
                    var candidate = TryFromDirectory(installLocation);
                    if (candidate is not null) return candidate;

                    var displayIcon = key?.GetValue("DisplayIcon") as string;
                    candidate = CleanExecutable(displayIcon);
                    if (candidate is not null &&
                        string.Equals(Path.GetFileName(candidate), "RTSS.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                // Fall through to common installation paths.
            }
        }

        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "RivaTuner Statistics Server",
                "RTSS.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "RivaTuner Statistics Server",
                "RTSS.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? TryFromDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return null;

        try
        {
            var candidate = Path.Combine(directory.Trim().Trim('"'), "RTSS.exe");
            return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? CleanExecutable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var cleaned = value.Trim().Trim('"');
        var comma = cleaned.LastIndexOf(',');
        if (comma > 2 && int.TryParse(cleaned[(comma + 1)..], out _))
        {
            cleaned = cleaned[..comma].Trim().Trim('"');
        }

        try
        {
            return File.Exists(cleaned) ? Path.GetFullPath(cleaned) : null;
        }
        catch
        {
            return null;
        }
    }
}
