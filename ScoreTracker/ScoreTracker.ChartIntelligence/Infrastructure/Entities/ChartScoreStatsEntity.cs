namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // Population score statistics per chart (variance source for the tier list page),
    // maintained by TierListSaga during the daily scores rebuild. Composite key
    // (MixId, ChartId) is configured in ChartIntelligenceModelContribution.
    internal class ChartScoreStatsEntity
    {
        public Guid MixId { get; set; }
        public Guid ChartId { get; set; }
        public double ScoreStandardDeviation { get; set; }
        public int ScoreCount { get; set; }
    }
}
