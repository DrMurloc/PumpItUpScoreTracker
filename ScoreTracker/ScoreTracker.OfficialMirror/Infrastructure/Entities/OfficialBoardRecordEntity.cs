using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     The record book for one chart board: its all-time high score across every sealed
///     snapshot. A grade band is claimed exactly when HighScore reaches its floor, so this
///     single number encodes every claimed band. Rebuildable by replaying history.
/// </summary>
internal sealed class OfficialBoardRecordEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int LeaderboardId { get; set; }

    public int HighScore { get; set; }
    public int AchievedSnapshotId { get; set; }
}
