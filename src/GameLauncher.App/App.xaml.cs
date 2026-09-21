using System.Windows;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Services;
using GameLauncher.Infrastructure.Adapters;
using GameLauncher.Infrastructure.Metadata;
using GameLauncher.Infrastructure.Repositories;
using GameLauncher.Infrastructure.Runtime;
using GameLauncher.Infrastructure.Storage;

namespace GameLauncher.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new AppPaths();
        paths.EnsureCreated();

        var repository = new SqliteGameRepository(paths.DatabasePath);
        IGameSourceAdapter[] adapters =
        [
            new SteamGameSourceAdapter(),
            new EpicGameSourceAdapter(),
            new GogGameSourceAdapter(),
            new EaGameSourceAdapter(),
            new UbisoftGameSourceAdapter(),
            new BattleNetGameSourceAdapter(),
            new XboxGameSourceAdapter()
        ];

        var metadata = new SteamArtworkMetadataEnricher(paths.CoversDirectory);
        var library = new GameLibraryService(repository, adapters, metadata);
        var sessions = new GameSessionService(repository, new WindowsGameRuntime());

        var window = new MainWindow(library, sessions);
        window.Show();
    }
}
