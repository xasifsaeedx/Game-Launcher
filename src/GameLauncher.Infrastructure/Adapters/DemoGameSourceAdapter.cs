using GameLauncher.Core.Adapters;
using GameLauncher.Core.Models;

namespace GameLauncher.Infrastructure.Adapters;

public sealed class DemoGameSourceAdapter : IGameSourceAdapter
{
    private static readonly Guid DemoGameId = Guid.Parse("6D2EE65C-8CBE-4597-BAED-5143405D02C1");
    private static readonly Guid DemoInstallationId = Guid.Parse("9073E78C-B31A-49C4-B2B1-872F4836D8DF");

    public string Id => "demo";
    public string DisplayName => "Phase 0 Demo Adapter";
    public GameSource Source => GameSource.Demo;

    public Task<IReadOnlyList<DiscoveredGame>> DiscoverInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var game = new Game(DemoGameId, "Phase 0 Test Game", now, now);
        var installation = new GameInstallation(
            DemoInstallationId,
            DemoGameId,
            Source,
            "phase0-demo",
            @"C:\Games\Phase0Test",
            @"C:\Games\Phase0Test\Phase0Test.exe",
            null,
            true);

        IReadOnlyList<DiscoveredGame> discovered = new[]
        {
            new DiscoveredGame(game, installation)
        };

        return Task.FromResult(discovered);
    }
}
