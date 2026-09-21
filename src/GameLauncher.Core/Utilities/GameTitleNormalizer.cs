using System.Text;
using System.Text.RegularExpressions;

namespace GameLauncher.Core.Utilities;

public static partial class GameTitleNormalizer
{
    public static string Normalize(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        var value = title
            .Replace("™", string.Empty, StringComparison.Ordinal)
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Replace("©", string.Empty, StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormKD);

        value = PlatformSuffixRegex().Replace(value, string.Empty);
        value = NonAlphaNumericRegex().Replace(value, " ");
        value = WhitespaceRegex().Replace(value, " ").Trim();

        return value.ToLowerInvariant();
    }

    [GeneratedRegex(@"\s*[\(\[]\s*(pc|windows|win32|win64)\s*[\)\]]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PlatformSuffixRegex();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
