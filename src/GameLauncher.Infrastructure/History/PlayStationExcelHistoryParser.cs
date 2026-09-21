using ClosedXML.Excel;
using GameLauncher.Core.History;
using GameLauncher.Core.Models;

namespace GameLauncher.Infrastructure.History;

public sealed class PlayStationExcelHistoryParser : IHistoryFileParser
{
    private static readonly string[] ExplicitTitleAliases =
        ["gametitle", "gamename", "titlename", "productname"];

    private static readonly string[] GeneralTitleAliases =
        ["title", "name"];

    private static readonly string[] IdAliases =
        ["titleid", "productid", "gameid", "conceptid", "npcommunicationid", "externalid"];

    private static readonly string[] PlatformAliases =
        ["platform", "console", "system", "device"];

    private static readonly string[] LastPlayedAliases =
        ["lastplayed", "lastplayeddate", "lastplayedutc", "mostrecentplay", "recentlyplayed"];

    public bool CanParse(GameSource source, string filePath) =>
        source == GameSource.PlayStation &&
        Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    public Task<IReadOnlyList<HistoryImportGame>> ParseAsync(
        GameSource source,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (source != GameSource.PlayStation)
        {
            throw new ArgumentException(
                "The PlayStation Excel importer only accepts PlayStation history.",
                nameof(source));
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(filePath);
        var games = new List<HistoryImportGame>();

        foreach (var worksheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var used = worksheet.RangeUsed();
            if (used is null) continue;

            var firstRow = used.RangeAddress.FirstAddress.RowNumber;
            var lastRow = used.RangeAddress.LastAddress.RowNumber;
            var firstColumn = used.RangeAddress.FirstAddress.ColumnNumber;
            var lastColumn = used.RangeAddress.LastAddress.ColumnNumber;

            var headerRow = 0;
            Dictionary<int, string>? headers = null;

            for (var row = firstRow; row <= Math.Min(lastRow, firstRow + 24); row++)
            {
                var candidate = new Dictionary<int, string>();
                for (var column = firstColumn; column <= lastColumn; column++)
                {
                    var header = GenericHistoryFileParser.NormalizeHeader(
                        worksheet.Cell(row, column).GetFormattedString());

                    if (!string.IsNullOrWhiteSpace(header))
                    {
                        candidate[column] = header;
                    }
                }

                if (FindTitleColumn(candidate, worksheet.Name) > 0 &&
                    LooksGameRelated(candidate, worksheet.Name))
                {
                    headerRow = row;
                    headers = candidate;
                    break;
                }
            }

            if (headerRow == 0 || headers is null) continue;

            var titleColumn = FindTitleColumn(headers, worksheet.Name);
            var idColumn = FindColumn(headers, IdAliases);
            var platformColumn = FindColumn(headers, PlatformAliases);
            var lastPlayedColumn = FindColumn(headers, LastPlayedAliases);
            var playtimeColumn = FindPlaytimeColumn(headers);

            for (var row = headerRow + 1; row <= lastRow; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var title = worksheet.Cell(row, titleColumn).GetFormattedString().Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                var externalId = Read(worksheet, row, idColumn) ?? string.Empty;
                var platform = Read(worksheet, row, platformColumn);
                var lastPlayed = GenericHistoryFileParser.ParseDate(
                    Read(worksheet, row, lastPlayedColumn));

                long? playtime = null;
                if (playtimeColumn > 0 &&
                    headers.TryGetValue(playtimeColumn, out var playtimeHeader))
                {
                    playtime = GenericHistoryFileParser.ParsePlaytime(
                        Read(worksheet, row, playtimeColumn),
                        playtimeHeader);
                }

                games.Add(new HistoryImportGame(
                    GameSource.PlayStation,
                    externalId,
                    title,
                    platform ?? "PlayStation",
                    playtime,
                    lastPlayed));
            }
        }

        if (games.Count == 0)
        {
            throw new InvalidOperationException(
                "No recognizable PlayStation game-history table was found in this Excel file. " +
                "The Sony export format may not include game history or may have changed.");
        }

        return Task.FromResult<IReadOnlyList<HistoryImportGame>>(games);
    }

    private static int FindTitleColumn(
        IReadOnlyDictionary<int, string> headers,
        string sheetName)
    {
        var explicitColumn = FindColumn(headers, ExplicitTitleAliases);
        if (explicitColumn > 0) return explicitColumn;

        if (!SheetNameLooksGameRelated(sheetName)) return -1;
        return FindColumn(headers, GeneralTitleAliases);
    }

    private static bool LooksGameRelated(
        IReadOnlyDictionary<int, string> headers,
        string sheetName)
    {
        if (SheetNameLooksGameRelated(sheetName)) return true;

        return headers.Values.Any(x =>
            x.Contains("playtime", StringComparison.Ordinal) ||
            x.Contains("timeplayed", StringComparison.Ordinal) ||
            IdAliases.Contains(x, StringComparer.Ordinal) ||
            PlatformAliases.Contains(x, StringComparer.Ordinal));
    }

    private static bool SheetNameLooksGameRelated(string sheetName)
    {
        var normalized = GenericHistoryFileParser.NormalizeHeader(sheetName);
        return normalized.Contains("game", StringComparison.Ordinal) ||
               normalized.Contains("play", StringComparison.Ordinal) ||
               normalized.Contains("title", StringComparison.Ordinal) ||
               normalized.Contains("product", StringComparison.Ordinal);
    }

    private static int FindColumn(
        IReadOnlyDictionary<int, string> headers,
        IEnumerable<string> aliases)
    {
        var set = aliases.ToHashSet(StringComparer.Ordinal);
        foreach (var pair in headers)
        {
            if (set.Contains(pair.Value)) return pair.Key;
        }

        return -1;
    }

    private static int FindPlaytimeColumn(IReadOnlyDictionary<int, string> headers)
    {
        foreach (var pair in headers)
        {
            if (pair.Value.Contains("playtime", StringComparison.Ordinal) ||
                pair.Value.Contains("timeplayed", StringComparison.Ordinal) ||
                pair.Value.Contains("hoursplayed", StringComparison.Ordinal) ||
                pair.Value.Contains("minutesplayed", StringComparison.Ordinal))
            {
                return pair.Key;
            }
        }

        return -1;
    }

    private static string? Read(
        IXLWorksheet worksheet,
        int row,
        int column)
    {
        if (column <= 0) return null;
        var value = worksheet.Cell(row, column).GetFormattedString().Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
