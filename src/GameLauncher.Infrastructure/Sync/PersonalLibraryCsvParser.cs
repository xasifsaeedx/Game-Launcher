using System.Globalization;
using System.Text;
using GameLauncher.Core.Models;

namespace GameLauncher.Infrastructure.Sync;

internal static class PersonalLibraryCsvParser
{
    public static IReadOnlyList<PersonalLibraryGame> Parse(
        string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<PersonalLibraryGame>();
        }

        var rows = ParseRows(csv);
        if (rows.Count == 0)
        {
            return Array.Empty<PersonalLibraryGame>();
        }

        var headers = BuildHeaderIndex(rows[0]);
        var titleIndex = FindHeader(
            headers,
            "title",
            "game",
            "gametitle",
            "gamename",
            "name");

        if (!titleIndex.HasValue)
        {
            throw new InvalidOperationException(
                "The sheet needs a Title or Game column.");
        }

        var platformsIndex = FindHeader(
            headers,
            "platforms",
            "platform");
        var steamIndex = FindHeader(
            headers,
            "steamappid",
            "steamid",
            "appid");
        var coverIndex = FindHeader(
            headers,
            "coverurl",
            "cover",
            "imageurl",
            "artwork");
        var rankIndex = FindHeader(
            headers,
            "rank",
            "rankscore",
            "nextplayrank",
            "priority");
        var statusIndex = FindHeader(
            headers,
            "status",
            "preference",
            "librarystatus");
        var progressIndex = FindHeader(
            headers,
            "progress",
            "progressstatus");
        var ratingIndex = FindHeader(
            headers,
            "rating",
            "score");

        var games = new List<PersonalLibraryGame>();
        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var title = Get(row, titleIndex);
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

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

    private static IReadOnlyDictionary<string, int> BuildHeaderIndex(
        IReadOnlyList<string> row) =>
        row
            .Select((value, index) => new
            {
                Key = NormalizeHeader(value),
                Index = index
            })
            .Where(x => x.Key.Length > 0)
            .GroupBy(x => x.Key)
            .ToDictionary(
                x => x.Key,
                x => x.First().Index,
                StringComparer.Ordinal);

    private static IReadOnlyList<string[]> ParseRows(string csv)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < csv.Length; index++)
        {
            var character = csv[index];

            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < csv.Length &&
                        csv[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(character);
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;

                case ',':
                    CompleteField(row, field);
                    break;

                case '\r':
                    if (index + 1 < csv.Length &&
                        csv[index + 1] == '\n')
                    {
                        index++;
                    }

                    CompleteRow(rows, row, field);
                    break;

                case '\n':
                    CompleteRow(rows, row, field);
                    break;

                default:
                    field.Append(character);
                    break;
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            CompleteRow(rows, row, field);
        }

        return rows;
    }

    private static void CompleteField(
        ICollection<string> row,
        StringBuilder field)
    {
        row.Add(field.ToString());
        field.Clear();
    }

    private static void CompleteRow(
        ICollection<string[]> rows,
        List<string> row,
        StringBuilder field)
    {
        CompleteField(row, field);
        rows.Add(row.ToArray());
        row.Clear();
    }

    private static int? FindHeader(
        IReadOnlyDictionary<string, int> headers,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (headers.TryGetValue(name, out var index))
            {
                return index;
            }
        }

        return null;
    }

    private static string NormalizeHeader(string value) =>
        new(
            (value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static string? Get(
        IReadOnlyList<string> row,
        int? index) =>
        index.HasValue &&
        index.Value >= 0 &&
        index.Value < row.Count
            ? row[index.Value]
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
