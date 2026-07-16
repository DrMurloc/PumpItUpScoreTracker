namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     One chart's official play-ranking place in one snapshot — the popularity board is
///     chart-ranked, unlike the player-ranked placement rows.
/// </summary>
internal sealed class OfficialChartPopularityEntity
{
    public int SnapshotId { get; set; }
    public Guid ChartId { get; set; }
    public int Place { get; set; }
}
