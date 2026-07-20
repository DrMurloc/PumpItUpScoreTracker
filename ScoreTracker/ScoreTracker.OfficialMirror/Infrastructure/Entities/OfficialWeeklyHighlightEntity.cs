using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     One editorial highlight row computed at import: a PUMBILITY mover, a
///     boards-climbed entry, a record-beating new #1, a chart/folder grade first — or
///     one of the hero's summary rows (weekly pulse, top gainers, debuts, floor marks),
///     which is why PlayerId is nullable. Co-credited firsts are one row per claimant
///     sharing the same subject columns. The generic Prev/New value pair carries ranks
///     for movers and counts for boards-climbed; Score carries the record score where
///     one exists; the per-kind packing of the summary rows is documented on
///     <see cref="Domain.HighlightKinds" />.
/// </summary>
internal sealed class OfficialWeeklyHighlightEntity
{
    [Key] public int Id { get; set; }
    public int SnapshotId { get; set; }
    public Guid MixId { get; set; }
    [MaxLength(30)] public string Kind { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? PlayerId { get; set; }
    public int? DethronedPlayerId { get; set; }
    public int? LeaderboardId { get; set; }
    public Guid? ChartId { get; set; }
    [MaxLength(20)] public string? ChartType { get; set; }
    public int? Level { get; set; }
    [MaxLength(10)] public string? GradeBand { get; set; }
    public decimal? Score { get; set; }
    public decimal? PrevValue { get; set; }
    public decimal? NewValue { get; set; }
}
