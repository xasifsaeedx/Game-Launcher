using Microsoft.Win32;

namespace GameLauncher.Infrastructure.Adapters;

public static class SteamLibraryLocator
{
    public static IReadOnlyList<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRegistryPath(roots, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        AddRegistryPath(roots, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        AddRegistryPath(roots, Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            AddDirectory(roots, Path.Combine(programFilesX86, "Steam"));
        }

        return roots.ToArray();
    }

    public static IReadOnlyList<string> FindLibraries(string steamRoot)
    {
        var libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFile)) return new[] { Path.GetFullPath(steamRoot) };

        try
        {
            return SteamLibraryParser.ParseLibraryPaths(File.ReadAllText(libraryFile), steamRoot)
                .Where(Directory.Exists)
                .ToArray();
        }
        catch
        {
            return new[] { Path.GetFullPath(steamRoot) };
        }
    }

    private static void AddRegistryPath(
        HashSet<string> roots,
        RegistryKey hive,
        string subKey,
        string valueName)
    {
        try
        {
            using var key = hive.OpenSubKey(subKey);
            AddDirectory(roots, key?.GetValue(valueName) as string);
        }
        catch
        {
            // Steam can still be located through the remaining fallbacks.
        }
    }

    private static void AddDirectory(HashSet<string> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var fullPath = Path.GetFullPath(path.Trim().Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(fullPath)) roots.Add(fullPath);
        }
        catch
        {
            // Ignore invalid registry/path values.
        }
    }
}
