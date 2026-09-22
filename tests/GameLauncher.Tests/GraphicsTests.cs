namespace GameLauncher.Tests;

internal static class GraphicsTests
{
    internal static Task GraphicsTierRecognizesGtx1660Super()
    {
        Assert.Equal(
            GraphicsPerformanceTier.Mainstream1080p,
            HardwareTierClassifier.ClassifyGpuName("EVGA NVIDIA GeForce GTX 1660 SUPER"));

        Assert.Equal(
            GraphicsPerformanceTier.Strong1080p,
            HardwareTierClassifier.ClassifyGpuName("Intel Arc B580"));

        Assert.Equal(
            GraphicsPerformanceTier.Unknown,
            HardwareTierClassifier.ClassifyGpuName("Future Mystery GPU"));

        Assert.Equal(
            "Manual",
            GraphicsRecommendationPolicy.RecommendPreset(
                GraphicsPerformanceTier.Unknown,
                GraphicsQualityPreference.Quality));

        return Task.CompletedTask;
    }

    internal static Task GraphicsRecommendationTargetsQuality()
    {
        var target = GraphicsOptimizerSettings.Default;
        Assert.Equal(1920, target.TargetWidth);
        Assert.Equal(1080, target.TargetHeight);
        Assert.Equal(60, target.TargetFps);
        Assert.Equal(GraphicsQualityPreference.Quality, target.QualityPreference);

        Assert.Equal(
            "High",
            GraphicsRecommendationPolicy.RecommendPreset(
                GraphicsPerformanceTier.Mainstream1080p,
                GraphicsQualityPreference.Quality));

        var tips = GraphicsRecommendationPolicy.BuildGeneralTips(
            GraphicsPerformanceTier.Mainstream1080p,
            target);

        Assert.True(tips.Any(x => x.Contains("Ray tracing: Off", StringComparison.Ordinal)));
        Assert.True(tips.Any(x => x.Contains("Textures: High", StringComparison.Ordinal)));
        return Task.CompletedTask;
    }

    internal static Task GraphicsCatalogMatchesSteamId()
    {
        var catalog = new GraphicsProfileCatalog(BuiltInGraphicsProfiles.Create());
        var now = DateTimeOffset.UtcNow;
        var game = new Game(Guid.NewGuid(), "Totally Different Local Title", now, now);
        var install = new GameInstallation(
            Guid.NewGuid(),
            game.Id,
            GameSource.Steam,
            "292030",
            @"C:\Games\Witcher3",
            @"C:\Games\Witcher3\witcher3.exe",
            "steam://rungameid/292030",
            true);

        var match = catalog.FindMatch(
            new GameLibraryItem(game, new[] { install }, 0, null));

        Assert.NotNull(match);
        Assert.Equal("witcher3", match!.Id);
        Assert.True(match.SupportsSafeApply);
        return Task.CompletedTask;
    }

    internal static async Task GraphicsOptimizerSettingsPersist()
    {
        using var temp = new TempDirectory();
        var repository = new SqliteGraphicsOptimizerRepository(
            Path.Combine(temp.Path, "launcher.db"));

        await repository.SaveSettingsAsync(
            new GraphicsOptimizerSettings(
                2560,
                1440,
                144,
                GraphicsQualityPreference.Balanced,
                GraphicsPerformanceTier.HighEnd,
                true));

        var stored = await repository.GetSettingsAsync();
        Assert.Equal(2560, stored.TargetWidth);
        Assert.Equal(1440, stored.TargetHeight);
        Assert.Equal(144, stored.TargetFps);
        Assert.Equal(GraphicsQualityPreference.Balanced, stored.QualityPreference);
        Assert.Equal<GraphicsPerformanceTier?>(GraphicsPerformanceTier.HighEnd, stored.TierOverride);
        Assert.True(stored.AutoApplyBeforeLaunch);
    }

    internal static async Task SafeIniGraphicsPatchBacksUpAndRestores()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "user.settings");
        const string original =
            "Resolution=1280x720\n" +
            "LimitFPS=30\n" +
            "TextureQuality=High\n";
        await File.WriteAllTextAsync(path, original);

        var profile = new GameGraphicsProfile(
            "test-ini",
            "Test INI",
            null,
            Array.Empty<string>(),
            path,
            GraphicsConfigFormat.IniKeyValue,
            new[]
            {
                new SafeGraphicsPatch("Resolution", "Resolution", "{width}x{height}"),
                new SafeGraphicsPatch("FPS limit", "LimitFPS", "{fps}"),
                new SafeGraphicsPatch("Missing", "ThisKeyDoesNotExist", "1")
            },
            Array.Empty<string>());

        var hardware = new HardwareProfile(
            "CPU",
            "GTX 1660 SUPER",
            32,
            1920,
            1080,
            60,
            GraphicsPerformanceTier.Mainstream1080p,
            DateTimeOffset.UtcNow);

        var target = GraphicsOptimizerSettings.Default;
        var recommendation = new GraphicsRecommendation(
            "Test INI",
            "High",
            GraphicsPerformanceTier.Mainstream1080p,
            target,
            profile,
            path,
            Array.Empty<string>(),
            profile.SafePatches);

        var runtime = new SafeGraphicsConfigRuntime();
        var applied = await runtime.ApplyAsync(recommendation, hardware);

        Assert.True(applied.Changed);
        Assert.True(File.Exists(path + ".game-launcher.bak"));
        Assert.Equal(original, await File.ReadAllTextAsync(path + ".game-launcher.bak"));

        var updated = await File.ReadAllTextAsync(path);
        Assert.True(updated.Contains("Resolution=1920x1080", StringComparison.Ordinal));
        Assert.True(updated.Contains("LimitFPS=60", StringComparison.Ordinal));
        Assert.True(updated.Contains("TextureQuality=High", StringComparison.Ordinal));
        Assert.True(!updated.Contains("ThisKeyDoesNotExist", StringComparison.Ordinal));
        Assert.True(applied.Warnings.Any(x => x.Contains("not present", StringComparison.Ordinal)));

        var secondTarget = target with { TargetWidth = 1600, TargetHeight = 900 };
        var secondRecommendation = recommendation with { Target = secondTarget };
        await runtime.ApplyAsync(secondRecommendation, hardware);
        Assert.Equal(original, await File.ReadAllTextAsync(path + ".game-launcher.bak"));

        var restored = await runtime.RestoreAsync(recommendation);
        Assert.True(restored.Changed);
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    internal static async Task SafeXmlGraphicsPatchChangesExistingFieldsOnly()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.xml");
        const string original =
            "<Settings><video>" +
            "<ScreenWidth value=\"1280\" />" +
            "<ScreenHeight value=\"720\" />" +
            "<RefreshRate value=\"60\" />" +
            "<VSync value=\"0\" />" +
            "<TextureQuality value=\"2\" />" +
            "</video></Settings>";
        await File.WriteAllTextAsync(path, original);

        var profile = new GameGraphicsProfile(
            "test-xml",
            "Test XML",
            null,
            Array.Empty<string>(),
            path,
            GraphicsConfigFormat.XmlValueAttribute,
            new[]
            {
                new SafeGraphicsPatch("Width", "ScreenWidth", "{width}"),
                new SafeGraphicsPatch("Height", "ScreenHeight", "{height}"),
                new SafeGraphicsPatch("VSync", "VSync", "{vsync}"),
                new SafeGraphicsPatch("Missing", "UnknownSetting", "1")
            },
            Array.Empty<string>());

        var hardware = new HardwareProfile(
            "CPU",
            "GTX 1660 SUPER",
            32,
            1920,
            1080,
            60,
            GraphicsPerformanceTier.Mainstream1080p,
            DateTimeOffset.UtcNow);

        var recommendation = new GraphicsRecommendation(
            "Test XML",
            "High",
            GraphicsPerformanceTier.Mainstream1080p,
            GraphicsOptimizerSettings.Default,
            profile,
            path,
            Array.Empty<string>(),
            profile.SafePatches);

        var runtime = new SafeGraphicsConfigRuntime();
        var applied = await runtime.ApplyAsync(recommendation, hardware);
        var updated = await File.ReadAllTextAsync(path);
        _ = XDocument.Parse(updated);

        Assert.True(applied.Changed);
        Assert.True(updated.Contains("ScreenWidth value=\"1920\"", StringComparison.Ordinal));
        Assert.True(updated.Contains("ScreenHeight value=\"1080\"", StringComparison.Ordinal));
        Assert.True(updated.Contains("VSync value=\"1\"", StringComparison.Ordinal));
        Assert.True(updated.Contains("TextureQuality value=\"2\"", StringComparison.Ordinal));
        Assert.True(!updated.Contains("UnknownSetting", StringComparison.Ordinal));
        Assert.True(applied.Warnings.Any(x => x.Contains("not present", StringComparison.Ordinal)));
    }
}
