using System.Globalization;
using System.Net;
using GameLauncher.Core.Models;
using GameLauncher.Core.Sync;

namespace GameLauncher.Infrastructure.Sync;

public sealed class GoogleSheetsPersonalLibraryClient : IPersonalLibraryClient
{
    private readonly HttpClient _http;

    public GoogleSheetsPersonalLibraryClient(HttpClient? httpClient = null)
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
        settings = settings.Normalize();

        if (!settings.IsConfigured)
        {
            throw new ArgumentException(
                "Enter a valid Google Sheets URL from docs.google.com.",
                nameof(settings));
        }

        var csvUri = BuildCsvUri(settings.SheetUrl);
        using var response = await _http.GetAsync(csvUri, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException(
                "Google Sheets denied access. Set the sheet to Viewer access for anyone with the link, or publish it to the web.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Google Sheets sync failed ({(int)response.StatusCode}).");
        }

        var csv = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(csv);
    }

    public static Uri BuildCsvUri(string sheetUrl)
    {
        if (!Uri.TryCreate(sheetUrl?.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "docs.google.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid Google Sheets URL.", nameof(sheetUrl));
        }

        if (uri.AbsolutePath.Contains("/spreadsheets/d/e/", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Contains("/pub", StringComparison.OrdinalIgnoreCase))
        {
            var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
            var url = uri.GetLeftPart(UriPartial.Path) + uri.Query;
            if (!uri.Query.Contains("output=csv", StringComparison.OrdinalIgnoreCase))
            {
                url += separator + "output=csv";
            }

            return new Uri(url);
        }

        const string marker = "/spreadsheets/d/";
        var markerIndex = uri.AbsolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            throw new ArgumentException("The URL is not a Google Sheets document.", nameof(sheetUrl));
        }

        var after = uri.AbsolutePath[(markerIndex + marker.Length)..];
        var slash = after.IndexOf('/');
        var id = slash >= 0 ? after[..slash] : after;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("The Google Sheets document ID is missing.", nameof(sheetUrl));
        }

        var gid = ReadParameter(uri.Query, "gid") ??
                  ReadParameter(uri.Fragment.TrimStart('#'), "gid") ??
                  "0";

        return new Uri(
            $"https://docs.google.com/spreadsheets/d/{id}/export?format=csv&gid={Uri.EscapeDataString(gid)}");
    }

    public static IReadOnlyList<PersonalLibraryGame> Parse(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<PersonalLibraryGame>();
        }

        var rows = ParseCsv(csv);
        if (rows.Count == 0)
        {
            return Array.Empty<PersonalLibraryGame>();
        }

        var headers = rows[0]
            .Select((value, index) => new
            {
                Key = NormalizeHeader(value),
                Index = index
            })
            .Where(x => x.Key.Length > 0)
            .GroupBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.First().Index, StringComparer.Ordinal);

        var titleIndex = FindHeader(headers, "title", "game", "gametitle", "gamename", "name");
        if (!titleIndex.HasValue)
        {
            throw new InvalidOperationException(
                "The sheet needs a Title or Game column.");
        }

        var platformsIndex = FindHeader(headers, "platforms", "platform");
        var steamIndex = FindHeader(headers, "steamappid", "steamid", "appid");
        var coverIndex = FindHeader(headers, "coverurl", "cover", "imageurl", "artwork");
        var rankIndex = FindHeader(headers, "rank", "rankscore", "nextplayrank", "priority");
        var statusIndex = FindHeader(headers, "status", "preference", "librarystatus");
        var progressIndex = FindHeader(headers, "progress", "progressstatus");
        var ratingIndex = FindHeader(headers, "rating", "score");

        var games = new List<PersonalLibraryGame>();
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var title = Get(row, titleIndex);
            if (string.IsNullOrWhiteSpace(title)) continue;

            games.Add(new PersonalLibraryGame(
                rowIndex + 1,
                title.Trim(),
                Get(row, platformsIndex)?.Trim() ?? string.Empty,
                ParseLong(Get(row, steamIndex)),
                NullIfWhiteSpace(Get(row, coverIndex)),
                ParseInt(Get(row, rankIndex)),
                NormalizeStatus(Get(row, statusIndex)),
                NormalizeStatus(Get(row, progressIndex)),
                ClampRating(ParseInt(Get(row, ratingIndex)))));
        }

        return games
            .OrderBy(x => x.Rank ?? int.MaxValue)
            .ThenBy(x => x.SourceRow)
            .ToArray();
    }

    private static IReadOnlyList<string[]> ParseCsv(string csv)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;

        for (var i = 0; i < csv.Length; i++)
        {
            var ch = csv[i];

            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    if (i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    row.Clear();
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row.ToArray());
                    row.Clear();
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static int? FindHeader(
        IReadOnlyDictionary<string, int> headers,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.TryGetValue(name, out var index)) return index;
        }

        return null;
    }

    private static string NormalizeHeader(string value) =>
        new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static string? Get(IReadOnlyList<string> row, int? index) =>
        index.HasValue && index.Value >= 0 && index.Value < row.Count
            ? row[index.Value]
            : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static int? ClampRating(int? value) =>
        value is >= 1 and <= 10 ? value : null;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeStatus(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? ReadParameter(string text, string name)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        foreach (var pair in text.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 &&
                string.Equals(parts[0], name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
    }
}
