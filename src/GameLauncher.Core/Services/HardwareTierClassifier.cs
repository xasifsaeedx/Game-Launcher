using GameLauncher.Core.Models;

namespace GameLauncher.Core.Services;

public static class HardwareTierClassifier
{
    public static GraphicsPerformanceTier ClassifyGpuName(string? gpuName)
    {
        var name = (gpuName ?? string.Empty).ToUpperInvariant();

        if (name.Contains("GTX 1660") || name.Contains("GTX 1070") || name.Contains("GTX 1080") ||
            name.Contains("RTX 2060") || name.Contains("RTX 3050") ||
            name.Contains("RX 5600") || name.Contains("RX 5700") ||
            name.Contains("RX 6600") || name.Contains("ARC A580") || name.Contains("ARC A750"))
        {
            return GraphicsPerformanceTier.Mainstream1080p;
        }

        if (name.Contains("GTX 1650") || name.Contains("GTX 1060") ||
            name.Contains("RX 5500") || name.Contains("RX 580") ||
            name.Contains("ARC A380"))
        {
            return GraphicsPerformanceTier.Entry1080p;
        }

        if (name.Contains("RTX 3060") || name.Contains("RTX 3070") ||
            name.Contains("RTX 4060") || name.Contains("RTX 5060") ||
            name.Contains("RX 6700") || name.Contains("RX 7600") ||
            name.Contains("RX 9060") || name.Contains("ARC A770") ||
            name.Contains("ARC B580"))
        {
            return GraphicsPerformanceTier.Strong1080p;
        }

        if (name.Contains("RTX 3080") || name.Contains("RTX 3090") ||
            name.Contains("RTX 4070") || name.Contains("RTX 5070") ||
            name.Contains("RX 6800") || name.Contains("RX 6900") ||
            name.Contains("RX 7700") || name.Contains("RX 7800") ||
            name.Contains("RX 9070"))
        {
            return GraphicsPerformanceTier.HighEnd;
        }

        if (name.Contains("RTX 4080") || name.Contains("RTX 4090") ||
            name.Contains("RTX 5080") || name.Contains("RTX 5090") ||
            name.Contains("RX 7900") || name.Contains("RX 9080"))
        {
            return GraphicsPerformanceTier.Enthusiast;
        }

        return GraphicsPerformanceTier.Unknown;
    }
}
