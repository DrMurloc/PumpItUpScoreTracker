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
}
