using System.Diagnostics;
using System.Net.Http.Json;
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
        var installerAsset = release.Assets?.FirstOrDefault(x =>
            string.Equals(
                x.Name,
                "GameLauncher-Setup.exe",
                StringComparison.OrdinalIgnoreCase));

        if (installerAsset?.BrowserDownloadUrl is not null &&
            Uri.TryCreate(installerAsset.BrowserDownloadUrl, UriKind.Absolute, out var parsed))
        {
            installer = parsed;
        }

        return new LauncherUpdateInfo(
            currentVersion,
            latest,
            release.Name ?? release.TagName ?? $"v{latest}",
            new Uri(release.HtmlUrl ?? "https://github.com/xasifsaeedx/Game-Launcher/releases"),
            installer,
            latest > currentVersion);
    }

    public async Task<string> DownloadInstallerAsync(
        LauncherUpdateInfo update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.InstallerDownload is null)
        {
            throw new InvalidOperationException(
                "This release does not contain GameLauncher-Setup.exe.");
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

        File.Move(temporary, path, overwrite: true);
        return path;
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
