namespace GameLauncher.Infrastructure.Storage;

public sealed class AppPaths
{
    private readonly string _baseDirectory;

    public AppPaths(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        RootDirectory = Path.Combine(_baseDirectory, "MyGameLauncher");
        DatabasePath = Path.Combine(RootDirectory, "launcher.db");
        LogsDirectory = Path.Combine(RootDirectory, "logs");
        CacheDirectory = Path.Combine(RootDirectory, "cache");
        CoversDirectory = Path.Combine(CacheDirectory, "covers");
    }

    public string RootDirectory { get; }
    public string DatabasePath { get; }
    public string LogsDirectory { get; }
    public string CacheDirectory { get; }
    public string CoversDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(CoversDirectory);
    }
}
