namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    /// <summary>
    ///     The season's balance snapshot, DELTA ROWS ONLY (docs/design/march-of-murlocs.md
    ///     §6/§9.3): a chart with no row here scores at its folder level + 0.5, which is
    ///     byte-identical to a stored override of exactly that value — so those rows are
    ///     never written.
    /// </summary>
    internal sealed class MoMChartLevelEntity
    {
        public Guid SeasonId { get; set; }
        public Guid MixId { get; set; }
        public Guid ChartId { get; set; }
        public double Level { get; set; }
    }
}
