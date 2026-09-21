namespace GameLauncher.Core.Models;

public sealed record GraphicsApplyResult(
    bool Changed,
    string? ConfigPath,
    string? BackupPath,
    IReadOnlyList<string> AppliedChanges,
    IReadOnlyList<string> Warnings);
