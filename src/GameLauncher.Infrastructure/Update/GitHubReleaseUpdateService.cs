using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using GameLauncher.Core.Models;
using GameLauncher.Core.Update;

namespace GameLauncher.Infrastructure.Update;

public sealed class GitHubReleaseUpdateService : ILauncherUpdateService
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/xasifsaeedx/Game-Launcher/releases/latest";

    private readonly HttpClient _http;

    public GitHubReleaseUpdateService(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<LauncherUpdateInfo> CheckAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.UserAgent.ParseAdd("GameLauncher/8.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Update check failed with HTTP {(int)response.StatusCode}.");
        }

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("GitHub returned an empty release response.");

        var tag = (release.TagName ?? string.Empty).Trim().TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var latest))
        {
            throw new InvalidOperationException(
                $"Latest release tag '{release.TagName}' is not a semantic version.");
        }

        Uri? installer = null;
        Uri? checksum = null;
        var installerAsset = release.Assets?.FirstOrDefault(x =>
            string.Equals(
                x.Name,
                "GameLauncher-Setup.exe",
                StringComparison.OrdinalIgnoreCase));
        var checksumAsset = release.Assets?.FirstOrDefault(x =>
            string.Equals(
                x.Name,
                "GameLauncher-Setup.exe.sha256",
                StringComparison.OrdinalIgnoreCase));

        if (installerAsset?.BrowserDownloadUrl is not null &&
            Uri.TryCreate(installerAsset.BrowserDownloadUrl, UriKind.Absolute, out var parsed) &&
            parsed.Scheme == Uri.UriSchemeHttps)
        {
            installer = parsed;
        }

        if (checksumAsset?.BrowserDownloadUrl is not null &&
            Uri.TryCreate(checksumAsset.BrowserDownloadUrl, UriKind.Absolute, out var checksumUri) &&
            checksumUri.Scheme == Uri.UriSchemeHttps)
        {
            checksum = checksumUri;
        }

        return new LauncherUpdateInfo(
            currentVersion,
            latest,
            release.Name ?? release.TagName ?? $"v{latest}",
            ParseReleasePage(release.HtmlUrl),
            installer,
            checksum,
            latest > currentVersion);
    }

    public async Task<string> DownloadInstallerAsync(
        LauncherUpdateInfo update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.InstallerDownload is null || update.ChecksumDownload is null)
        {
            throw new InvalidOperationException(
                "This release does not contain a verifiable GameLauncher-Setup.exe and SHA-256 checksum.");
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "MyGameLauncher",
            "updates",
            update.LatestVersion.ToString());
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "GameLauncher-Setup.exe");
        var temporary = path + ".download";

        using var response = await _http.GetAsync(
            update.InstallerDownload,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = File.Create(temporary))
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        var expectedHash = await DownloadExpectedHashAsync(
            update.ChecksumDownload,
            cancellationToken);
        var actualHash = await ComputeSha256Async(temporary, cancellationToken);

        if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporary);
            throw new InvalidOperationException(
                "Downloaded installer failed SHA-256 verification.");
        }

        File.Move(temporary, path, overwrite: true);
        return path;
    }

    private async Task<string> DownloadExpectedHashAsync(
        Uri checksumUri,
        CancellationToken cancellationToken)
    {
        var text = await _http.GetStringAsync(checksumUri, cancellationToken);
        var hash = text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (hash is null || hash.Length != 64 || !hash.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException("Release SHA-256 checksum is invalid.");
        }

        return hash;
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static Uri ParseReleasePage(string? value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri;
        }

        return new Uri("https://github.com/xasifsaeedx/Game-Launcher/releases");
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string? TagName,
        string? Name,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        IReadOnlyList<GitHubAsset>? Assets);

    private sealed record GitHubAsset(
        string? Name,
        [property: JsonPropertyName("browser_download_url")] string? BrowserDownloadUrl);
}
