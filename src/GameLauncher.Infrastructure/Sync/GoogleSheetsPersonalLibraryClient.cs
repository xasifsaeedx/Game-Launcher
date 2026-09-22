using System.Net;
using GameLauncher.Core.Models;
using GameLauncher.Core.Sync;

namespace GameLauncher.Infrastructure.Sync;

public sealed class GoogleSheetsPersonalLibraryClient : IPersonalLibraryClient
{
    private readonly HttpClient _http;

    public GoogleSheetsPersonalLibraryClient(
        HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<IReadOnlyList<PersonalLibraryGame>> GetGamesAsync(
        PersonalLibrarySettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();
        if (!normalized.IsConfigured)
        {
            throw new ArgumentException(
                "Enter a valid Google Sheets URL from docs.google.com.",
                nameof(settings));
        }

        var csvUri = BuildCsvUri(normalized.SheetUrl);
        using var response = await _http.GetAsync(
            csvUri,
            cancellationToken);

        if (response.StatusCode is
            HttpStatusCode.Unauthorized or
            HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                "Google Sheets denied access. Set the sheet to Viewer access for anyone with the link, or publish it to the web.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google Sheets sync failed ({(int)response.StatusCode}).");
        }

        var csv = await response.Content.ReadAsStringAsync(
            cancellationToken);

        return Parse(csv);
    }

    public static Uri BuildCsvUri(string sheetUrl)
    {
        if (!Uri.TryCreate(
                sheetUrl?.Trim(),
                UriKind.Absolute,
                out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                uri.Host,
                "docs.google.com",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Invalid Google Sheets URL.",
                nameof(sheetUrl));
        }

        if (uri.AbsolutePath.Contains(
                "/spreadsheets/d/e/",
                StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Contains(
                "/pub",
                StringComparison.OrdinalIgnoreCase))
        {
            return BuildPublishedCsvUri(uri);
        }

        return BuildDocumentCsvUri(uri, sheetUrl);
    }

    public static IReadOnlyList<PersonalLibraryGame> Parse(
        string csv) =>
        PersonalLibraryCsvParser.Parse(csv);

    private static Uri BuildPublishedCsvUri(Uri uri)
    {
        var separator = string.IsNullOrEmpty(uri.Query)
            ? "?"
            : "&";

        var url = uri.GetLeftPart(UriPartial.Path) + uri.Query;
        if (!uri.Query.Contains(
                "output=csv",
                StringComparison.OrdinalIgnoreCase))
        {
            url += separator + "output=csv";
        }

        return new Uri(url);
    }

    private static Uri BuildDocumentCsvUri(
        Uri uri,
        string sheetUrl)
    {
        const string marker = "/spreadsheets/d/";
        var markerIndex = uri.AbsolutePath.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);

        if (markerIndex < 0)
        {
            throw new ArgumentException(
                "The URL is not a Google Sheets document.",
                nameof(sheetUrl));
        }

        var documentPath = uri.AbsolutePath[
            (markerIndex + marker.Length)..];

        var separatorIndex = documentPath.IndexOf('/');
        var documentId = separatorIndex >= 0
            ? documentPath[..separatorIndex]
            : documentPath;

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException(
                "The Google Sheets document ID is missing.",
                nameof(sheetUrl));
        }

        var gid = ReadParameter(uri.Query, "gid") ??
                  ReadParameter(
                      uri.Fragment.TrimStart('#'),
                      "gid") ??
                  "0";

        return new Uri(
            $"https://docs.google.com/spreadsheets/d/{documentId}/export?format=csv&gid={Uri.EscapeDataString(gid)}");
    }

    private static string? ReadParameter(
        string text,
        string name)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var pair in text
                     .TrimStart('?')
                     .Split(
                         '&',
                         StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 &&
                string.Equals(
                    parts[0],
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }
}
