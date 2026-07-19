using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     The record books (per-board and per-folder all-time highs) and the weekly
///     highlight rows the import computes from them. Both are derived state: a rebuild
///     resets and replays every sealed snapshot.
/// </summary>
internal interface IOfficialRecordRepository
{
    Task<IReadOnlyList<BoardRecordRow>> GetBoardRecords(MixEnum mix, CancellationToken ct);
    Task UpsertBoardRecords(IReadOnlyCollection<BoardRecordRow> records, CancellationToken ct);
    Task<IReadOnlyList<FolderRecordRow>> GetFolderRecords(MixEnum mix, CancellationToken ct);

    /// <summary>
    ///     Best-ever chart and folder highs from every mix EXCEPT the given one — the
    ///     cross-mix reference for world-first suppression.
    /// </summary>
    Task<CrossMixRecordHighs> GetCrossMixHighs(MixEnum mix, CancellationToken ct);
    Task UpsertFolderRecords(MixEnum mix, IReadOnlyCollection<FolderRecordRow> records, CancellationToken ct);
    Task ResetRecords(MixEnum mix, CancellationToken ct);

    Task WriteHighlights(int snapshotId, MixEnum mix, IReadOnlyCollection<HighlightRow> rows, CancellationToken ct);
    Task<IReadOnlyList<HighlightRow>> GetHighlights(int snapshotId, CancellationToken ct);
    Task DeleteHighlights(MixEnum mix, CancellationToken ct);
}
