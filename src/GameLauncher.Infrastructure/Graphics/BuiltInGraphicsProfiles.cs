using GameLauncher.Core.Models;

namespace GameLauncher.Infrastructure.Graphics;

public static class BuiltInGraphicsProfiles
{
    public static IReadOnlyList<GameGraphicsProfile> Create() =>
    [
        new(
            "witcher3",
            "The Witcher 3: Wild Hunt",
            292030,
            ["The Witcher 3", "The Witcher 3: Wild Hunt - Complete Edition"],
            @"{documents}\The Witcher 3\user.settings",
            GraphicsConfigFormat.IniKeyValue,
            [
                new("Resolution", "Resolution", "{width}x{height}"),
                new("FPS limit", "LimitFPS", "{fps}")
            ],
            [
                "HairWorks: Off or Geralt only when 60 FPS matters.",
                "Foliage visibility and shadows are better performance levers than texture quality.",
                "Next-gen ray tracing should remain off on mainstream 1080p hardware."
            ]),
        new(
            "gta5",
            "Grand Theft Auto V",
            271590,
            ["GTA V", "Grand Theft Auto V Legacy"],
            @"{documents}\Rockstar Games\GTA V\settings.xml",
            GraphicsConfigFormat.XmlValueAttribute,
            [
                new("Screen width", "ScreenWidth", "{width}"),
                new("Screen height", "ScreenHeight", "{height}"),
                new("Refresh rate", "RefreshRate", "{refresh}"),
                new("VSync", "VSync", "{vsync}")
            ],
            [
                "Keep MSAA off before reducing textures.",
                "Use High/Very High shadows rather than the most expensive extended shadow options.",
                "Advanced Graphics distance/shadow options should be the first extras disabled for a stable target."
            ]),
        new(
            "rdr2",
            "Red Dead Redemption 2",
            1174180,
            ["Red Dead Redemption 2"],
            @"{documents}\Rockstar Games\Red Dead Redemption 2\Settings\system.xml",
            GraphicsConfigFormat.XmlValueAttribute,
            [
                new("Screen width", "screenWidth", "{width}"),
                new("Screen height", "screenHeight", "{height}"),
                new("Windowed width", "screenWidthWindowed", "{width}"),
                new("Windowed height", "screenHeightWindowed", "{height}"),
                new("Refresh rate", "refreshRateNumerator", "{refresh}"),
                new("Refresh denominator", "refreshRateDenominator", "1"),
                new("VSync", "vSync", "{vsync}")
            ],
            [
                "Keep MSAA off; TAA is substantially cheaper.",
                "Use Medium volumetrics and water physics before reducing textures.",
                "Tree tessellation is a low-value setting when the 60 FPS target is tight."
            ]),
        RecommendationOnly(
            "control",
            "Control: Ultimate Edition",
            870780,
            ["Control"],
            [
                "Ray tracing: Off on GTX-class hardware.",
                "Use native 1080p first; reduce render resolution only if the target cannot be held.",
                "Volumetrics and reflections are better settings to reduce before textures."
            ]),
        RecommendationOnly(
            "god-of-war",
            "God of War",
            1593500,
            [],
            [
                "Start at the recommended preset, then lower shadows/reflections before textures.",
                "Use a quality upscaler only when native resolution cannot hold the FPS target."
            ]),
        RecommendationOnly(
            "ghost-tsushima",
            "Ghost of Tsushima DIRECTOR'S CUT",
            2215430,
            ["Ghost of Tsushima"],
            [
                "Keep texture quality high when VRAM allows; reduce shadows and volumetrics first.",
                "Use the Quality upscaling mode before dropping the base quality preset."
            ]),
        RecommendationOnly(
            "ac-shadows",
            "Assassin's Creed Shadows",
            3159330,
            [],
            [
                "Use dynamic resolution or a quality upscaler when necessary for the frame-rate target.",
                "Ray tracing and dense environmental effects should be reduced before texture quality."
            ]),
        RecommendationOnly(
            "black-myth-wukong",
            "Black Myth: Wukong",
            2358720,
            [],
            [
                "Ray tracing: Off on mainstream 1080p hardware.",
                "Use a quality upscaler before reducing textures; shadows and global illumination are higher-cost levers."
            ])
    ];

    private static GameGraphicsProfile RecommendationOnly(
        string id,
        string title,
        long? steamAppId,
        IReadOnlyList<string> alternateTitles,
        IReadOnlyList<string> tips) =>
        new(
            id,
            title,
            steamAppId,
            alternateTitles,
            null,
            null,
            Array.Empty<SafeGraphicsPatch>(),
            tips);
}
