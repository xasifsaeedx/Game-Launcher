namespace GameLauncher.Tests;

internal readonly record struct LauncherTestCase(
    string Name,
    Func<Task> Run);

internal static class LauncherTestSuite
{
    public static IReadOnlyList<LauncherTestCase> All { get; } =
    [
        new("AppPaths creates expected folders", FoundationTests.AppPathsCreatesExpectedFolders),
        new("Stable IDs are deterministic", FoundationTests.StableIdsAreDeterministic),
        new("Title normalization is conservative", FoundationTests.TitleNormalizationIsConservative),

        new("SQLite repository round-trips launcher fields", RepositoryTests.RepositoryRoundTripsFields),
        new("SQLite repository totals completed play sessions", RepositoryTests.RepositoryTotalsPlaySessions),

        new("Steam manifest parser reads required fields", DiscoveryTests.SteamManifestParserReadsFields),
        new("Steam library parser reads extra libraries", DiscoveryTests.SteamLibraryParserReadsPaths),
        new("Epic manifest parser reads installed game metadata", DiscoveryTests.EpicManifestParserReadsFields),
        new("Epic adapter discovers local installation", DiscoveryTests.EpicAdapterDiscoversInstallation),
        new("Xbox adapter discovers XboxGames installation", DiscoveryTests.XboxAdapterDiscoversInstallation),
        new("Library service adds a manual game", DiscoveryTests.LibraryServiceAddsManualGame),
        new("Source sync remains idempotent", DiscoveryTests.SourceSyncRemainsIdempotent),
        new("Source rescan retires removed installations", DiscoveryTests.SourceRescanRetiresRemovedInstallations),
        new("Unified library merges exact normalized titles", DiscoveryTests.UnifiedLibraryMergesExactTitles),
        new("Steam artwork enricher caches cover art", DiscoveryTests.SteamArtworkEnricherCachesCover),

        new("Launch profile repository persists profile and actions", LaunchTests.LaunchProfileRepositoryPersistsProfileAndActions),
        new("Launch profile service keeps one default across merged installs", LaunchTests.LaunchProfileServiceKeepsOneDefault),
        new("Smart launch orders actions and cleans companions", LaunchTests.SmartLaunchOrdersActionsAndCleansCompanions),
        new("Session service persists runtime playtime", LaunchTests.SessionServicePersistsPlaytime),

        new("Overlay formatter emits requested telemetry", OverlayTests.OverlayFormatterEmitsRequestedTelemetry),
        new("Overlay settings persist and normalize", OverlayTests.OverlaySettingsPersistAndNormalize),
        new("Overlay service skips disabled overlay", OverlayTests.OverlayServiceSkipsDisabledOverlay),
        new("Session service owns overlay lifecycle", OverlayTests.SessionServiceOwnsOverlayLifecycle),

        new("Personal library settings require Google Sheets HTTPS", PersonalLibraryTests.SettingsRequireGoogleSheetsHttps),
        new("Personal library matcher prefers Steam app ID", PersonalLibraryTests.MatcherPrefersSteamId),
        new("Personal library matcher falls back to normalized title", PersonalLibraryTests.MatcherFallsBackToTitle),
        new("Google Sheets URL converts to CSV export", PersonalLibraryTests.GoogleSheetsUrlConvertsToCsvExport),
        new("Google Sheets library parser reads flexible columns", PersonalLibraryTests.GoogleSheetsLibraryParserReadsFlexibleColumns),
        new("Game preferences persist favorites", PersonalLibraryTests.GamePreferencesPersistFavorites),
        new("Game preferences persist ratings", PersonalLibraryTests.GamePreferencesPersistRatings),
        new("Game preference schema migrates ratings", PersonalLibraryTests.GamePreferenceSchemaMigratesRatings),
        new("Library presentation policy handles search and filters", PersonalLibraryTests.LibraryPresentationPolicyHandlesSearchAndFilters),

        new("Graphics tier recognizes GTX 1660 Super", GraphicsTests.GraphicsTierRecognizesGtx1660Super),
        new("Graphics recommendation targets 1080p quality safely", GraphicsTests.GraphicsRecommendationTargetsQuality),
        new("Graphics catalog matches Steam ID before title", GraphicsTests.GraphicsCatalogMatchesSteamId),
        new("Graphics optimizer settings persist", GraphicsTests.GraphicsOptimizerSettingsPersist),
        new("Safe INI graphics patch backs up and restores", GraphicsTests.SafeIniGraphicsPatchBacksUpAndRestores),
        new("Safe XML graphics patch changes existing fields only", GraphicsTests.SafeXmlGraphicsPatchChangesExistingFieldsOnly),

        new("Steam history API parses owned games and playtime", HistoryTests.SteamHistoryApiParsesOwnedGames),
        new("Steam history credentials are protected at rest", HistoryTests.SteamHistoryCredentialsAreProtectedAtRest),
        new("Generic CSV history importer reads flexible columns", HistoryTests.GenericCsvHistoryImporterReadsFlexibleColumns),
        new("Generic JSON history importer reads games array", HistoryTests.GenericJsonHistoryImporterReadsGamesArray),
        new("Generic JSON history importer skips unrelated arrays", HistoryTests.GenericJsonHistoryImporterSkipsUnrelatedArrays),
        new("PlayStation Excel history importer reads game worksheet", HistoryTests.PlayStationExcelHistoryImporterReadsGameWorksheet),
        new("History repository keeps largest account snapshot", HistoryTests.HistoryRepositoryKeepsLargestSnapshot),
        new("History service keeps account and launcher playtime separate", HistoryTests.HistoryServiceKeepsPlaytimeSeparate),

        new("Launcher backup creates usable ZIP", MaintenanceTests.LauncherBackupCreatesUsableZip),
        new("History CSV export escapes values", MaintenanceTests.HistoryCsvExportEscapesValues),
        new("GitHub updater parses latest release", MaintenanceTests.GitHubUpdaterParsesLatestRelease),
        new("GitHub updater rejects non-HTTPS installer assets", MaintenanceTests.GitHubUpdaterRejectsHttpInstaller),
        new("GitHub updater downloads installer asset", MaintenanceTests.GitHubUpdaterDownloadsInstaller)
    ];
}
