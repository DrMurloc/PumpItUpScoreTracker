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

    /// <summary>
    ///     Highlights carry the same reading flag as placements, and for the same reason: a
    ///     snapshot has an official week and a supplemented one, and the two sets never mix on
    ///     a page. Both parameters are explicit rather than defaulted — see
    ///     <see cref="PlacementScope" />.
    /// </summary>
    Task WriteHighlights(int snapshotId, MixEnum mix, IReadOnlyCollection<HighlightRow> rows,
        bool isSupplemented, CancellationToken ct);

    Task<IReadOnlyList<HighlightRow>> GetHighlights(int snapshotId, bool isSupplemented, CancellationToken ct);
    Task DeleteHighlights(MixEnum mix, CancellationToken ct);

    /// <summary>
    ///     Clears one snapshot's supplemented highlights so a re-run replaces its own output.
    ///     The official rows are untouched — they belong to the sweep, not to the roll-up.
    /// </summary>
    Task DeleteSupplementedHighlights(int snapshotId, CancellationToken ct);
}
