using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     The record book for one folder (mix + chart type + level, e.g. every D26 board):
///     the all-time high score across the folder's boards. Same claimed-band encoding as
///     the per-board record.
/// </summary>
internal sealed class OfficialFolderRecordEntity
{
    public Guid MixId { get; set; }
    [MaxLength(20)] public string ChartType { get; set; } = string.Empty;
    public int Level { get; set; }
    public int HighScore { get; set; }
    public int AchievedSnapshotId { get; set; }
}
