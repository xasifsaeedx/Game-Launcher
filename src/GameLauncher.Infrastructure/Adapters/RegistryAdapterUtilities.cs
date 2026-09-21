using Microsoft.Win32;

namespace GameLauncher.Infrastructure.Adapters;

internal static class RegistryAdapterUtilities
{
    public static IEnumerable<(string SubKeyName, RegistryKey Key)> EnumerateSubKeys(
        RegistryHive hive,
        RegistryView view,
        string path)
    {
        RegistryKey? baseKey = null;
        RegistryKey? root = null;

        try
        {
            baseKey = RegistryKey.OpenBaseKey(hive, view);
            root = baseKey.OpenSubKey(path);
            if (root is null) yield break;

            foreach (var name in root.GetSubKeyNames())
            {
                RegistryKey? key = null;
                try
                {
                    key = root.OpenSubKey(name);
                    if (key is not null) yield return (name, key);
                }
                finally
                {
                    key?.Dispose();
                }
            }
        }
        finally
        {
            root?.Dispose();
            baseKey?.Dispose();
        }
    }

    public static string? ReadString(RegistryKey key, params string[] names)
    {
        foreach (var name in names)
        {
            if (key.GetValue(name) is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    public static string? FindUninstallDisplayName(string keySuffix)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var (_, key) in EnumerateSubKeys(
                         RegistryHive.LocalMachine,
                         view,
                         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
            {
                var name = ReadString(key, "DisplayName");
                if (string.IsNullOrWhiteSpace(name)) continue;

                var uninstallString = ReadString(key, "UninstallString") ?? string.Empty;
                var installLocation = ReadString(key, "InstallLocation") ?? string.Empty;
                if (key.Name.Contains(keySuffix, StringComparison.OrdinalIgnoreCase) ||
                    uninstallString.Contains(keySuffix, StringComparison.OrdinalIgnoreCase) ||
                    installLocation.Contains(keySuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
        }

        return null;
    }
}
