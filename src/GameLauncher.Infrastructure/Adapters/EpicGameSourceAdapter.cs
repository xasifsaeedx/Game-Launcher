using System.Text.Json;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;
using GameLauncher.Core.Utilities;

namespace GameLauncher.Infrastructure.Adapters;

public sealed class EpicGameSourceAdapter : IGameSourceAdapter
{
    private readonly string _manifestDirectory;

    public EpicGameSourceAdapter(string? programDataPath = null)
    {
        var programData = programDataPath
            ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        _manifestDirectory = Path.Combine(
            programData,
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");
    }

    public string Id => "epic";
    public string DisplayName => "Epic Games";
    public GameSource Source => GameSource.Epic;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_manifestDirectory))
        {
            return Task.FromResult<IReadOnlyList<DiscoveredGame>>(Array.Empty<DiscoveredGame>());
        }

        var games = new List<DiscoveredGame>();
        foreach (var file in Directory.EnumerateFiles(_manifestDirectory, "*.item", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var parsed = ParseManifest(File.ReadAllText(file));
                if (parsed is null || !Directory.Exists(parsed.InstallLocation)) continue;

                var now = DateTimeOffset.UtcNow;
                var externalId = parsed.CatalogItemId ?? parsed.AppName;
                var gameId = StableId.FromText($"epic-game:{externalId}");
                var installId = StableId.FromText($"epic-install:{externalId}");
                var executable = GameDiscoveryUtilities.CleanExecutablePath(
                    parsed.LaunchExecutable,
                    parsed.InstallLocation);

                var launchUri = !string.IsNullOrWhiteSpace(parsed.AppName)
                    ? $"com.epicgames.launcher://apps/{Uri.EscapeDataString(parsed.AppName)}?action=launch&silent=true"
                    : null;

                var game = new Game(
                    gameId,
                    parsed.DisplayName,
                    now,
                    now,
                    GameDiscoveryUtilities.FindLocalArtwork(parsed.InstallLocation));

                var installation = new GameInstallation(
                    installId,
                    gameId,
                    Source,
                    externalId,
                    parsed.InstallLocation,
                    executable,
                    launchUri,
                    true,
                    parsed.LaunchCommand);

                games.Add(new DiscoveredGame(game, installation));
            }
            catch
            {
                // One damaged Epic manifest must not break the rest of the library.
            }
        }

        return Task.FromResult<IReadOnlyList<DiscoveredGame>>(games);
    }

    public static EpicManifest? ParseManifest(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var displayName = GetString(root, "DisplayName");
        var installLocation = GetString(root, "InstallLocation");
        var appName = GetString(root, "AppName");

        if (string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(installLocation) ||
            string.IsNullOrWhiteSpace(appName))
        {
            return null;
        }

        return new EpicManifest(
            displayName,
            installLocation,
            appName,
            GetString(root, "CatalogItemId"),
            GetString(root, "LaunchExecutable"),
            GetString(root, "LaunchCommand"));
    }

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed record EpicManifest(
    string DisplayName,
    string InstallLocation,
    string AppName,
    string? CatalogItemId,
    string? LaunchExecutable,
    string? LaunchCommand);
