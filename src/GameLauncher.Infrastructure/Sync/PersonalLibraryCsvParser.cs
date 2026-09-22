using System.Globalization;
using GameLauncher.Core.Models;
using Microsoft.VisualBasic.FileIO;

namespace GameLauncher.Infrastructure.Sync;

internal static class PersonalLibraryCsvParser
{
    private static readonly string[] TitleAliases =
        ["title", "game", "gametitle", "gamename", "name"];

    private static readonly string[] PlatformAliases =
        ["platforms", "platform"];

    private static readonly string[] SteamIdAliases =
        ["steamappid", "steamid", "appid"];

    private static readonly string[] CoverAliases =
        ["coverurl", "cover", "imageurl", "artwork"];

    private static readonly string[] RankAliases =
        ["rank", "rankscore", "nextplayrank", "priority"];

    private static readonly string[] StatusAliases =
        ["status", "preference", "librarystatus"];

    private static readonly string[] ProgressAliases =
        ["progress", "progressstatus"];

    private static readonly string[] RatingAliases =
        ["rating", "score"];

    public static IReadOnlyList<PersonalLibraryGame> Parse(
        string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<PersonalLibraryGame>();
        }

        using var textReader = new StringReader(csv);
        using var parser = new TextFieldParser(textReader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            return Array.Empty<PersonalLibraryGame>();
        }

        var headers = parser.ReadFields() ?? Array.Empty<string>();
        var normalizedHeaders = headers
            .Select(NormalizeHeader)
            .ToArray();

        var titleIndex = FindIndex(
            normalizedHeaders,
            TitleAliases);

        if (titleIndex < 0)
        {
            throw new InvalidOperationException(
                "The sheet needs a Title or Game column.");
        }

        var platformIndex = FindIndex(
            normalizedHeaders,
            PlatformAliases);
        var steamIdIndex = FindIndex(
            normalizedHeaders,
            SteamIdAliases);
        var coverIndex = FindIndex(
            normalizedHeaders,
            CoverAliases);
        var rankIndex = FindIndex(
            normalizedHeaders,
            RankAliases);
        var statusIndex = FindIndex(
            normalizedHeaders,
            StatusAliases);
        var progressIndex = FindIndex(
            normalizedHeaders,
            ProgressAliases);
        var ratingIndex = FindIndex(
            normalizedHeaders,
            RatingAliases);

        var games = new List<PersonalLibraryGame>();
        var sourceRow = 1;

        while (!parser.EndOfData)
        {
            sourceRow++;

            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                continue;
            }

            if (fields is null ||
                titleIndex >= fields.Length)
            {
                continue;
            }

            var title = fields[titleIndex]?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            games.Add(new PersonalLibraryGame(
                sourceRow,
                title,
                Read(fields, platformIndex) ?? string.Empty,
                ParseLong(Read(fields, steamIdIndex)),
                NullIfWhiteSpace(Read(fields, coverIndex)),
                ParseInt(Read(fields, rankIndex)),
                NormalizeStatus(Read(fields, statusIndex)),
                NormalizeStatus(Read(fields, progressIndex)),
                ClampRating(ParseInt(Read(fields, ratingIndex)))));
        }

        return games
            .OrderBy(x => x.Rank ?? int.MaxValue)
            .ThenBy(x => x.SourceRow)
            .ToArray();
    }

    private static int FindIndex(
        IReadOnlyList<string> headers,
        IReadOnlyCollection<string> aliases)
    {
        for (var index = 0; index < headers.Count; index++)
        {
            if (aliases.Contains(headers[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static string NormalizeHeader(string value) =>
        new(
            (value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static string? Read(
        IReadOnlyList<string> row,
        int index) =>
        index >= 0 && index < row.Count
            ? row[index]
            : null;

    private static int? ParseInt(string? value) =>
        int.TryParse(
            value?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static long? ParseLong(string? value) =>
        long.TryParse(
            value?.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;

    private static int? ClampRating(int? value) =>
        value is >= 1 and <= 10
            ? value
            : null;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static string? NormalizeStatus(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
}
