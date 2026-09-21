namespace GameLauncher.Core.Models;

public sealed record Game(
    Guid Id,
    string Title,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
