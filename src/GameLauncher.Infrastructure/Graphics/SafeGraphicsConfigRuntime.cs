using System.Text.RegularExpressions;
using System.Xml.Linq;
using GameLauncher.Core.Graphics;
using GameLauncher.Core.Models;

namespace GameLauncher.Infrastructure.Graphics;

public sealed class SafeGraphicsConfigRuntime : IGraphicsConfigRuntime
{
    public async Task<GraphicsApplyResult> ApplyAsync(
        GraphicsRecommendation recommendation,
        HardwareProfile hardware,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(hardware);

        if (!recommendation.CanApplyAutomatically ||
            recommendation.Profile?.ConfigFormat is null ||
            string.IsNullOrWhiteSpace(recommendation.ConfigPath))
        {
            return new GraphicsApplyResult(
                false,
                recommendation.ConfigPath,
                null,
                Array.Empty<string>(),
                ["This game has no verified safe automatic config profile."]);
        }

        var path = recommendation.ConfigPath;
        if (!File.Exists(path))
        {
            return new GraphicsApplyResult(
                false,
                path,
                null,
                Array.Empty<string>(),
                ["Configuration file was not found. Launch the game once and save its graphics settings first."]);
        }

        var original = await File.ReadAllTextAsync(path, cancellationToken);
        var values = recommendation.SafePatches.ToDictionary(
            x => x.Key,
            x => ResolveTemplate(x.ValueTemplate, recommendation.Target, hardware),
            StringComparer.OrdinalIgnoreCase);

        var displayNames = recommendation.SafePatches.ToDictionary(
            x => x.Key,
            x => x.DisplayName,
            StringComparer.OrdinalIgnoreCase);

        var warnings = new List<string>();
        var applied = new List<string>();

        var updated = recommendation.Profile.ConfigFormat.Value switch
        {
            GraphicsConfigFormat.IniKeyValue =>
                PatchIni(original, values, displayNames, applied, warnings),
            GraphicsConfigFormat.XmlValueAttribute =>
                PatchXml(original, values, displayNames, applied, warnings),
            _ => original
        };

        if (string.Equals(original, updated, StringComparison.Ordinal))
        {
            return new GraphicsApplyResult(
                false,
                path,
                null,
                applied,
                warnings.Count == 0 ? ["Safe settings already match the target."] : warnings);
        }

        var backup = path + ".game-launcher.bak";
        if (!File.Exists(backup))
        {
            File.Copy(path, backup, overwrite: false);
        }

        var temporary = path + ".game-launcher.tmp";
        await File.WriteAllTextAsync(temporary, updated, cancellationToken);
        File.Move(temporary, path, overwrite: true);

        return new GraphicsApplyResult(true, path, backup, applied, warnings);
    }

    public Task<GraphicsApplyResult> RestoreAsync(
        GraphicsRecommendation recommendation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(recommendation.ConfigPath))
        {
            return Task.FromResult(
                new GraphicsApplyResult(
                    false,
                    null,
                    null,
                    Array.Empty<string>(),
                    ["This game has no automatic config profile."]));
        }

        var backup = recommendation.ConfigPath + ".game-launcher.bak";
        if (!File.Exists(backup))
        {
            return Task.FromResult(
                new GraphicsApplyResult(
                    false,
                    recommendation.ConfigPath,
                    backup,
                    Array.Empty<string>(),
                    ["No Game Launcher backup exists for this configuration file."]));
        }

        File.Copy(backup, recommendation.ConfigPath, overwrite: true);
        return Task.FromResult(
            new GraphicsApplyResult(
                true,
                recommendation.ConfigPath,
                backup,
                ["Original configuration restored."],
                Array.Empty<string>()));
    }

    private static string PatchIni(
        string text,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> displayNames,
        ICollection<string> applied,
        ICollection<string> warnings)
    {
        var updated = text;

        foreach (var (key, desired) in values)
        {
            var pattern = $"(?im)^(?<prefix>\\s*{Regex.Escape(key)}\\s*=\\s*)(?<value>.*)$";
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            var match = regex.Match(updated);

            if (!match.Success)
            {
                warnings.Add($"{displayNames[key]} was not changed because key '{key}' was not present.");
                continue;
            }

            var oldValue = match.Groups["value"].Value.Trim();
            if (string.Equals(oldValue, desired, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            updated = regex.Replace(
                updated,
                m => m.Groups["prefix"].Value + desired,
                count: 1);
            applied.Add($"{displayNames[key]}: {oldValue} -> {desired}");
        }

        return updated;
    }

    private static string PatchXml(
        string text,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyDictionary<string, string> displayNames,
        ICollection<string> applied,
        ICollection<string> warnings)
    {
        try
        {
            _ = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            warnings.Add($"XML could not be parsed safely: {ex.Message}");
            return text;
        }

        var updated = text;

        foreach (var (key, desired) in values)
        {
            var pattern =
                $"(?is)(<\\s*{Regex.Escape(key)}\\b[^>]*?\\bvalue\\s*=\\s*[\\\"'])(?<value>[^\\\"']*)([\\\"'])";
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            var matches = regex.Matches(updated);

            if (matches.Count != 1)
            {
                warnings.Add(
                    matches.Count == 0
                        ? $"{displayNames[key]} was not changed because element '{key}' with a value attribute was not present."
                        : $"{displayNames[key]} was not changed because element '{key}' was ambiguous.");
                continue;
            }

            var match = matches[0];
            var oldValue = match.Groups["value"].Value;
            if (string.Equals(oldValue, desired, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            updated = regex.Replace(
                updated,
                m => m.Groups[1].Value + desired + m.Groups[3].Value,
                count: 1);

            applied.Add($"{displayNames[key]}: {oldValue} -> {desired}");
        }

        return updated;
    }

    private static string ResolveTemplate(
        string template,
        GraphicsOptimizerSettings target,
        HardwareProfile hardware)
    {
        var refresh = hardware.RefreshRateHz > 0
            ? hardware.RefreshRateHz
            : target.TargetFps;
        var vsync = target.TargetFps == refresh ? "1" : "0";

        return template
            .Replace("{width}", target.TargetWidth.ToString(), StringComparison.Ordinal)
            .Replace("{height}", target.TargetHeight.ToString(), StringComparison.Ordinal)
            .Replace("{fps}", target.TargetFps.ToString(), StringComparison.Ordinal)
            .Replace("{refresh}", refresh.ToString(), StringComparison.Ordinal)
            .Replace("{vsync}", vsync, StringComparison.Ordinal);
    }
}
