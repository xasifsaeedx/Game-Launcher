namespace GameLauncher.Core.Models;

public sealed record OverlayRuntimeStatus(
    bool IsAvailable,
    string Message,
    string? RivaTunerPath = null);
