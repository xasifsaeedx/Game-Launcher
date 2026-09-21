using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameLauncher.Core.History;
using GameLauncher.Core.Models;
using Microsoft.VisualBasic.FileIO;

namespace GameLauncher.Infrastructure.History;

public sealed class GenericHistoryFileParser : IHistoryFileParser
{
    private static readonly string[] TitleAliases =
        ["title", "gametitle", "game", "gamename", "productname", "name"];

    private static readonly string[] StrongTitleAliases =
        ["title", "gametitle", "game", "gamename", "productname"];

    private static readonly string[] IdAliases =
        ["externalid", "gameid", "appid", "applicationid", "productid", "titleid", "id"];

    private static readonly string[] PlatformAliases =
        ["platform", "system", "device", "console"];

    private static readonly string[] LastPlayedAliases =
        ["lastplayed", "lastplayeddate", "lastplayedutc", "mostrecentplay", "recentlyplayed"];

    public bool CanParse(GameSource source, string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyList<HistoryImportGame>> ParseAsync(
        GameSource source,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? ParseJsonAsync(source, filePath, cancellationToken)
            : Task.FromResult(ParseCsv(source, filePath, cancellationToken));
    }

    private static IReadOnlyList<HistoryImportGame> ParseCsv(
        GameSource source,
        string filePath,
        CancellationToken cancellationToken)
    {
        using var parser = new TextFieldParser(filePath)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData) return Array.Empty<HistoryImportGame>();

        var headers = parser.ReadFields() ?? Array.Empty<string>();
        var normalized = headers.Select(NormalizeHeader).ToArray();
        var titleIndex = FindIndex(normalized, TitleAliases);

        if (titleIndex < 0)
        {
            throw new InvalidOperationException(
                "CSV does not contain a recognizable game-title column.");
        }

        var idIndex = FindIndex(normalized, IdAliases);
        var platformIndex = FindIndex(normalized, PlatformAliases);
        var lastPlayedIndex = FindIndex(normalized, LastPlayedAliases);
        var playtimeIndex = FindPlaytimeIndex(normalized);

        var games = new List<HistoryImportGame>();

        while (!parser.EndOfData)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                continue;
            }

            if (fields is null || titleIndex >= fields.Length) continue;
            var title = fields[titleIndex]?.Trim();
            if (string.IsNullOrWhiteSpace(title)) continue;

            games.Add(new HistoryImportGame(
                source,
                Read(fields, idIndex) ?? string.Empty,
                title,
                Read(fields, platformIndex),
                playtimeIndex >= 0
                    ? ParsePlaytime(
                        Read(fields, playtimeIndex),
                        normalized[playtimeIndex])
                    : null,
                ParseDate(Read(fields, lastPlayedIndex))));
        }

        return games;
    }

    private static async Task<IReadOnlyList<HistoryImportGame>> ParseJsonAsync(
        GameSource source,
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        var candidates = FindObjectArray(document.RootElement);
        if (candidates.Count == 0) return Array.Empty<HistoryImportGame>();

        var games = new List<HistoryImportGame>();

        foreach (var element in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (element.ValueKind != JsonValueKind.Object) continue;

            var properties = element.EnumerateObject()
                .ToDictionary(
                    x => NormalizeHeader(x.Name),
                    x => x.Value,
                    StringComparer.Ordinal);

            var title = ReadString(properties, TitleAliases);
            if (string.IsNullOrWhiteSpace(title)) continue;

            var playtimeProperty = properties.FirstOrDefault(x =>
                x.Key.Contains("playtime", StringComparison.Ordinal) ||
                x.Key.Contains("timeplayed", StringComparison.Ordinal) ||
                x.Key.Contains("hoursplayed", StringComparison.Ordinal) ||
                x.Key.Contains("minutesplayed", StringComparison.Ordinal));

            games.Add(new HistoryImportGame(
                source,
                ReadString(properties, IdAliases) ?? string.Empty,
                title.Trim(),
                ReadString(properties, PlatformAliases),
                playtimeProperty.Key is null
                    ? null
                    : ParsePlaytime(
                        ElementToString(playtimeProperty.Value),
                        playtimeProperty.Key),
                ParseDate(ReadString(properties, LastPlayedAliases))));
        }

        return games;
    }

    private static IReadOnlyList<JsonElement> FindObjectArray(
        JsonElement root,
        string? contextName = null)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            var values = root.EnumerateArray().ToArray();
            var context = NormalizeHeader(contextName);
            var allowsGenericName =
                context.Contains("game", StringComparison.Ordinal) ||
                context.Contains("library", StringComparison.Ordinal) ||
                context.Contains("product", StringComparison.Ordinal) ||
                context.Contains("title", StringComparison.Ordinal);

            var aliases = allowsGenericName ? TitleAliases : StrongTitleAliases;

            if (values.Any(x =>
                x.ValueKind == JsonValueKind.Object &&
                x.EnumerateObject().Any(p =>
                    aliases.Contains(NormalizeHeader(p.Name), StringComparer.Ordinal))))
            {
                return values;
            }

            foreach (var value in values)
            {
                var nested = FindObjectArray(value, contextName);
                if (nested.Count > 0) return nested;
            }

            return Array.Empty<JsonElement>();
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<JsonElement>();
        }

        foreach (var property in root.EnumerateObject())
        {
            var nested = FindObjectArray(property.Value, property.Name);
            if (nested.Count > 0) return nested;
        }

        return Array.Empty<JsonElement>();
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, JsonElement> properties,
        IEnumerable<string> aliases)
    {
        foreach (var alias in aliases)
        {
            if (properties.TryGetValue(alias, out var value))
            {
                return ElementToString(value);
            }
        }

        return null;
    }

    private static string? ElementToString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };

    internal static long? ParsePlaytime(string? value, string normalizedHeader)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var span) &&
            value.Contains(':'))
        {
            return Math.Max(0, (long)span.TotalSeconds);
        }

        if (double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var numeric))
        {
            if (normalizedHeader.Contains("second", StringComparison.Ordinal))
                return Math.Max(0, (long)Math.Round(numeric));
            if (normalizedHeader.Contains("minute", StringComparison.Ordinal))
                return Math.Max(0, (long)Math.Round(numeric * 60d));
            if (normalizedHeader.Contains("hour", StringComparison.Ordinal))
                return Math.Max(0, (long)Math.Round(numeric * 3600d));

            return null;
        }

        var totalSeconds = 0d;
        var matched = false;

        foreach (Match match in Regex.Matches(
                     value,
                     @"(?<number>d+(?:.d+)?)s*(?<unit>hours?|hrs?|h|minutes?|mins?|m|seconds?|secs?|s)",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            if (!double.TryParse(
                    match.Groups["number"].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var number))
            {
                continue;
            }

            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            totalSeconds += unit.StartsWith("h", StringComparison.Ordinal)
                ? number * 3600d
                : unit.StartsWith("m", StringComparison.Ordinal)
                    ? number * 60d
                    : number;
            matched = true;
        }

        return matched ? Math.Max(0, (long)Math.Round(totalSeconds)) : null;
    }

    internal static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTimeOffset.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    internal static string NormalizeHeader(string? value) =>
        new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static int FindIndex(
        IReadOnlyList<string> headers,
        IEnumerable<string> aliases)
    {
        var set = aliases.ToHashSet(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            if (set.Contains(headers[i])) return i;
        }

        return -1;
    }

    private static int FindPlaytimeIndex(IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (headers[i].Contains("playtime", StringComparison.Ordinal) ||
                headers[i].Contains("timeplayed", StringComparison.Ordinal) ||
                headers[i].Contains("hoursplayed", StringComparison.Ordinal) ||
                headers[i].Contains("minutesplayed", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static string? Read(IReadOnlyList<string> fields, int index) =>
        index >= 0 && index < fields.Count && !string.IsNullOrWhiteSpace(fields[index])
            ? fields[index].Trim()
            : null;
}
