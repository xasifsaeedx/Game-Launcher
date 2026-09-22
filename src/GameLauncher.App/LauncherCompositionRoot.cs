using GameLauncher.Core.Adapters;
using GameLauncher.Core.History;
using GameLauncher.Core.Services;
using GameLauncher.Infrastructure.Adapters;
using GameLauncher.Infrastructure.Backup;
using GameLauncher.Infrastructure.Graphics;
using GameLauncher.Infrastructure.Hardware;
using GameLauncher.Infrastructure.History;
using GameLauncher.Infrastructure.Metadata;
using GameLauncher.Infrastructure.Overlay;
using GameLauncher.Infrastructure.Repositories;
using GameLauncher.Infrastructure.Runtime;
using GameLauncher.Infrastructure.Security;
using GameLauncher.Infrastructure.Storage;
using GameLauncher.Infrastructure.Sync;
using GameLauncher.Infrastructure.Update;

namespace GameLauncher.App;

internal static class LauncherCompositionRoot
{
    public static MainWindow CreateMainWindow()
    {
        var paths = new AppPaths();
        paths.EnsureCreated();

        var gameRepository = new SqliteGameRepository(paths.DatabasePath);
        var profileRepository = new SqliteLaunchProfileRepository(paths.DatabasePath);
        var overlayRepository = new SqliteOverlaySettingsRepository(paths.DatabasePath);
        var graphicsRepository = new SqliteGraphicsOptimizerRepository(paths.DatabasePath);
        var preferenceRepository = new SqliteGamePreferenceRepository(paths.DatabasePath);
        var personalLibraryRepository = new SqlitePersonalLibraryRepository(paths.DatabasePath);
        var historyRepository = new SqliteGameHistoryRepository(paths.DatabasePath);

        var library = new GameLibraryService(
            gameRepository,
            CreateGameSourceAdapters(),
            new SteamArtworkMetadataEnricher(paths.CoversDirectory));

        var graphics = new GraphicsOptimizerService(
            graphicsRepository,
            new WindowsHardwareProfileDetector(),
            new GraphicsProfileCatalog(BuiltInGraphicsProfiles.Create()),
            new SafeGraphicsConfigRuntime());

        var overlay = new GameplayOverlayService(
            overlayRepository,
            new RtssGameplayOverlayRuntime());

        var sessions = new GameSessionService(
            gameRepository,
            new WindowsGameRuntime(),
            overlay);

        var profiles = new LaunchProfileService(profileRepository);
        var smartLaunch = new SmartLaunchService(
            profiles,
            sessions,
            new WindowsExternalProgramRuntime());

        var personalLibrary = new PersonalLibraryService(
            personalLibraryRepository,
            new GoogleSheetsPersonalLibraryClient(),
            library);

        var secretProtector = new DpapiSecretProtector();
        var history = new GameHistoryService(
            historyRepository,
            new SqliteSteamHistorySettingsRepository(
                paths.DatabasePath,
                secretProtector),
            new SteamHistoryApiClient(),
            CreateHistoryParsers(),
            library);

        var preferences = new GamePreferenceService(preferenceRepository);
        var play = new LauncherPlayService(
            smartLaunch,
            graphics,
            personalLibrary);

        var stats = new LauncherStatsService(
            library,
            history,
            preferences,
            personalLibrary);

        return new MainWindow(
            library,
            play,
            profiles,
            overlay,
            personalLibrary,
            graphics,
            history,
            preferences,
            stats,
            paths,
            new LauncherBackupService(),
            new GitHubReleaseUpdateService());
    }

    private static IGameSourceAdapter[] CreateGameSourceAdapters() =>
    [
        new SteamGameSourceAdapter(),
        new EpicGameSourceAdapter(),
        new GogGameSourceAdapter(),
        new EaGameSourceAdapter(),
        new UbisoftGameSourceAdapter(),
        new BattleNetGameSourceAdapter(),
        new XboxGameSourceAdapter()
    ];

    private static IHistoryFileParser[] CreateHistoryParsers() =>
    [
        new PlayStationExcelHistoryParser(),
        new GenericHistoryFileParser()
    ];
}
