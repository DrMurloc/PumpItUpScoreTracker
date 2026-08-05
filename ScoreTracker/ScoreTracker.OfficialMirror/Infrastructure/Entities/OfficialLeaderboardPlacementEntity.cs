namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     One player's place on one board in one snapshot. Keyed (clustered) by
///     (SnapshotId, LeaderboardId, Place, PlayerId) so writes append in order and a board
///     reads back in display order; the (PlayerId, SnapshotId) index serves player
///     timelines and search. Score is decimal to keep official PUMBILITY cents exact —
///     chart scores are whole numbers within the same range.
/// </summary>
internal sealed class OfficialLeaderboardPlacementEntity
{
    public int SnapshotId { get; set; }
    public int LeaderboardId { get; set; }
    public int PlayerId { get; set; }
    public int Place { get; set; }
    public decimal Score { get; set; }

    /// <summary>
    ///     False for a row piugame published, true for one rolled up from a linked public
    ///     player's own ledger. Every official read filters this to false, and only
    ///     <see cref="Infrastructure.EFOfficialSnapshotRepository" /> is allowed to touch this
    ///     table at all — the two together are what keep supplemented rows out of the record
    ///     books, the tier-list feed, the cutlines and the Discord digest
    ///     (supplemented-leaderboards.md §7).
    ///     <para>
    ///         Place on a supplemented row is its merged place at roll-up time. Supplemented
    ///         reads re-rank anyway, because on a rating board an official row's merged place
    ///         differs from the official place stored against it.
    ///     </para>
    /// </summary>
    public bool IsSupplemented { get; set; }
}
