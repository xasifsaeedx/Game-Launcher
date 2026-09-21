using System.Windows;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Services;
using GameLauncher.Infrastructure.Adapters;
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
        IGameSourceAdapter[] adapters = [new SteamGameSourceAdapter()];
        var library = new GameLibraryService(repository, adapters);
        var sessions = new GameSessionService(repository, new WindowsGameRuntime());

        var window = new MainWindow(library, sessions);
        window.Show();
    }
}
