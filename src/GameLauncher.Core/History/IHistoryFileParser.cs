using GameLauncher.Core.Models;

namespace GameLauncher.Core.History;

public interface IHistoryFileParser
{
    bool CanParse(GameSource source, string filePath);

    Task<IReadOnlyList<HistoryImportGame>> ParseAsync(
        GameSource source,
        string filePath,
        CancellationToken cancellationToken = default);
}
