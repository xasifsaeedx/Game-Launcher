using System.IO.Compression;
using System.Text;
using GameLauncher.Core.Models;
using GameLauncher.Infrastructure.Storage;
using Microsoft.Data.Sqlite;

namespace GameLauncher.Infrastructure.Backup;

public sealed class LauncherBackupService
{
    public async Task CreateBackupAsync(
        AppPaths paths,
        string destinationZip,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZip);

        paths.EnsureCreated();
        destinationZip = Path.GetFullPath(destinationZip);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationZip)!);

        var temporary = destinationZip + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);

        var databaseSnapshot = await CreateDatabaseSnapshotAsync(
            paths.DatabasePath,
            cancellationToken);

        try
        {
            await using (var output = File.Create(temporary))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var file in Directory.EnumerateFiles(
                             paths.RootDirectory,
                             "*",
                             SearchOption.AllDirectories))
                {
                cancellationToken.ThrowIfCancellationRequested();

                    var full = Path.GetFullPath(file);
                    if (string.Equals(full, paths.DatabasePath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(full, paths.DatabasePath + "-wal", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(full, paths.DatabasePath + "-shm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(full, destinationZip, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(full, temporary, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(paths.RootDirectory, full);
                var entry = archive.CreateEntry(relative, CompressionLevel.Optimal);

                await using var input = new FileStream(
                    full,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                await using var entryStream = entry.Open();
                    await input.CopyToAsync(entryStream, cancellationToken);
                }

                if (databaseSnapshot is not null)
                {
                    var entry = archive.CreateEntry("launcher.db", CompressionLevel.Optimal);
                    await using var input = File.OpenRead(databaseSnapshot);
                    await using var entryStream = entry.Open();
                    await input.CopyToAsync(entryStream, cancellationToken);
                }
            }

            File.Move(temporary, destinationZip, overwrite: true);
        }
        finally
        {
            if (databaseSnapshot is not null)
            {
                try { File.Delete(databaseSnapshot); } catch { }
            }
        }
    }

    private static async Task<string?> CreateDatabaseSnapshotAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(databasePath)) return null;

        var snapshot = Path.Combine(
            Path.GetTempPath(),
            $"GameLauncher-backup-{Guid.NewGuid():N}.db");

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };
        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = snapshot,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        await using var source = new SqliteConnection(sourceBuilder.ToString());
        await using var destination = new SqliteConnection(destinationBuilder.ToString());
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        return snapshot;
    }

    public async Task ExportHistoryCsvAsync(
        IReadOnlyList<GamingHistoryItem> history,
        string destinationCsv,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationCsv);

        destinationCsv = Path.GetFullPath(destinationCsv);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationCsv)!);

        await using var writer = new StreamWriter(
            destinationCsv,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        await writer.WriteLineAsync(
            "Title,Sources,Account Playtime Seconds,Launcher Tracked Seconds,Last Played UTC,Installed");

        foreach (var item in history.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sources = string.Join(
                " + ",
                item.Sources.Select(x => x.ToString()));

            var line = string.Join(
                ",",
                Csv(item.Title),
                Csv(sources),
                item.KnownAccountPlaytimeSeconds.ToString(),
                item.LauncherTrackedSeconds.ToString(),
                Csv(item.LastPlayedUtc?.ToUniversalTime().ToString("O") ?? string.Empty),
                item.IsInstalled ? "true" : "false");

            await writer.WriteLineAsync(line);
        }
    }

    private static string Csv(string value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
