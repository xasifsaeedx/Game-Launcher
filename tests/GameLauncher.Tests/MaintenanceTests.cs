namespace GameLauncher.Tests;

internal static class MaintenanceTests
{
    internal static async Task LauncherBackupCreatesUsableZip()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        paths.EnsureCreated();

        var repository = new SqliteGameRepository(paths.DatabasePath);
        var now = DateTimeOffset.UtcNow;
        await repository.UpsertGameAsync(
            new Game(Guid.NewGuid(), "Backup Test", now, now));

        var cover = Path.Combine(paths.CoversDirectory, "cover.txt");
        await File.WriteAllTextAsync(cover, "cover-bytes");

        var destination = Path.Combine(temp.Path, "backup.zip");
        var service = new LauncherBackupService();
        await service.CreateBackupAsync(paths, destination);

        Assert.True(File.Exists(destination));

        using var archive = ZipFile.OpenRead(destination);
        var entries = archive.Entries
            .Select(x => x.FullName.Replace('\\', '/'))
            .ToArray();

        Assert.True(entries.Contains("launcher.db"));
        Assert.True(entries.Contains("cache/covers/cover.txt"));
    }

    internal static async Task HistoryCsvExportEscapesValues()
    {
        using var temp = new TempDirectory();
        var destination = Path.Combine(temp.Path, "history.csv");
        var now = DateTimeOffset.UtcNow;

        GamingHistoryItem[] history =
        [
            new(
                "Game, \"Deluxe\"",
                new[]
                {
                    new GameHistoryRecord(
                        Guid.NewGuid(),
                        GameSource.PlayStation,
                        "ps-test",
                        "Game, \"Deluxe\"",
                        "PS5",
                        3600,
                        now,
                        now)
                },
                false,
                120,
                now)
        ];

        var service = new LauncherBackupService();
        await service.ExportHistoryCsvAsync(history, destination);

        var text = await File.ReadAllTextAsync(destination);
        Assert.True(text.Contains("\"Game, \"\"Deluxe\"\"\"", StringComparison.Ordinal));
        Assert.True(text.Contains(",3600,120,", StringComparison.Ordinal));
    }

    internal static async Task GitHubUpdaterParsesLatestRelease()
    {
        const string json =
            """
            {
              "tag_name": "v1.1.0",
              "name": "My Game Launcher v1.1.0",
              "html_url": "https://github.com/xasifsaeedx/Game-Launcher/releases/tag/v1.1.0",
              "assets": [
                {
                  "name": "GameLauncher-Setup.exe",
                  "browser_download_url": "https://example.test/GameLauncher-Setup.exe"
                },
                {
                  "name": "GameLauncher-Setup.exe.sha256",
                  "browser_download_url": "https://example.test/GameLauncher-Setup.exe.sha256"
                }
              ]
            }
            """;

        using var http = new HttpClient(
            new RoutingHttpHandler(Encoding.UTF8.GetBytes(json), [1, 2, 3]));
        var service = new GitHubReleaseUpdateService(http);

        var update = await service.CheckAsync(new Version(1, 0, 0));

        Assert.True(update.IsUpdateAvailable);
        Assert.Equal(new Version(1, 1, 0), update.LatestVersion);
        Assert.NotNull(update.InstallerDownload);
        Assert.NotNull(update.ChecksumDownload);
        Assert.Equal("GameLauncher-Setup.exe", Path.GetFileName(update.InstallerDownload!.AbsolutePath));
    }

    internal static async Task GitHubUpdaterRejectsHttpInstaller()
    {
        const string json =
            """
            {
              "tag_name": "v1.1.0",
              "name": "My Game Launcher v1.1.0",
              "html_url": "http://example.test/release",
              "assets": [
                {
                  "name": "GameLauncher-Setup.exe",
                  "browser_download_url": "http://example.test/GameLauncher-Setup.exe"
                }
              ]
            }
            """;

        using var http = new HttpClient(
            new RoutingHttpHandler(Encoding.UTF8.GetBytes(json), [1, 2, 3]));
        var service = new GitHubReleaseUpdateService(http);
        var update = await service.CheckAsync(new Version(1, 0, 0));

        Assert.True(update.IsUpdateAvailable);
        Assert.True(update.InstallerDownload is null);
        Assert.Equal(
            "https://github.com/xasifsaeedx/Game-Launcher/releases",
            update.ReleasePage.ToString().TrimEnd('/'));
    }

    internal static async Task GitHubUpdaterDownloadsInstaller()
    {
        const string json =
            """
            {
              "tag_name": "v1.1.0",
              "name": "My Game Launcher v1.1.0",
              "html_url": "https://github.com/xasifsaeedx/Game-Launcher/releases/tag/v1.1.0",
              "assets": [
                {
                  "name": "GameLauncher-Setup.exe",
                  "browser_download_url": "https://example.test/GameLauncher-Setup.exe"
                },
                {
                  "name": "GameLauncher-Setup.exe.sha256",
                  "browser_download_url": "https://example.test/GameLauncher-Setup.exe.sha256"
                }
              ]
            }
            """;

        byte[] installer = [10, 20, 30, 40, 50];
        var checksum = Encoding.ASCII.GetBytes(
            Convert.ToHexString(SHA256.HashData(installer)).ToLowerInvariant() +
            "  GameLauncher-Setup.exe\n");
        using var http = new HttpClient(
            new RoutingHttpHandler(Encoding.UTF8.GetBytes(json), installer, checksum));
        var service = new GitHubReleaseUpdateService(http);
        var update = await service.CheckAsync(new Version(1, 0, 0));

        var path = await service.DownloadInstallerAsync(update);
        try
        {
            Assert.True(File.Exists(path));
            Assert.SequenceEqual(installer, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
