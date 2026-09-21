using System.Windows;
using GameLauncher.Core.Adapters;
using GameLauncher.Core.Services;
using GameLauncher.Infrastructure.Adapters;
using GameLauncher.Infrastructure.Metadata;
using GameLauncher.Infrastructure.Overlay;
using GameLauncher.Infrastructure.Repositories;
using GameLauncher.Infrastructure.Runtime;
using GameLauncher.Infrastructure.Security;
using GameLauncher.Infrastructure.Storage;
using GameLauncher.Infrastructure.Sync;

namespace GameLauncher.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = new AppPaths();
        paths.EnsureCreated();

        var repository = new SqliteGameRepository(paths.DatabasePath);
        var launchProfilesRepository = new SqliteLaunchProfileRepository(paths.DatabasePath);
        var overlaySettingsRepository = new SqliteOverlaySettingsRepository(paths.DatabasePath);
        var hatchableRepository = new SqliteHatchableSyncRepository(
            paths.DatabasePath,
            new DpapiSecretProtector());

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
        var overlay = new GameplayOverlayService(
            overlaySettingsRepository,
            new RtssGameplayOverlayRuntime());
        var sessions = new GameSessionService(
            repository,
            new WindowsGameRuntime(),
            overlay);
        var profiles = new LaunchProfileService(launchProfilesRepository);
        var smartLaunch = new SmartLaunchService(
            profiles,
            sessions,
            new WindowsExternalProgramRuntime());

        var hatchable = new HatchableSyncService(
            hatchableRepository,
            new HatchableApiClient(),
            library);

        var window = new MainWindow(
            library,
            smartLaunch,
            profiles,
            overlay,
            hatchable);
        window.Show();
    }
}
