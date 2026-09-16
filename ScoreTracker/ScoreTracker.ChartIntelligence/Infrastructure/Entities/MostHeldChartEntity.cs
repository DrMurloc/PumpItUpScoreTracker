namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // The difficulty glow's green end: each folder's most-held charts, the other end of the same
    // weekly census the Hardmode list comes from (docs/design/hardmode-leaderboard.md D32). Exactly
    // HardmodeChart's shape, season key included, so a season roll can copy both lists the same way
    // (docs/design/seasons.md D31). Display only: nothing is priced against it.
    // No UserId: this is a chart-level fact about a population, not about anyone.
    // Composite key configured in ChartIntelligenceModelContribution.
    internal class MostHeldChartEntity
    {
        // 0 = the census's all-time list, otherwise a season's copy (docs/design/seasons.md D31, D34).
        public short SeasonId { get; set; }
        public Guid MixId { get; set; }
        public Guid ChartId { get; set; }
        public int Level { get; set; }
        // The weighted hold count, 51 − slot summed over every full pool that holds it.
        public double Points { get; set; }
        // Distinct players holding it anywhere, so three pools of one player count once.
        public int Holders { get; set; }
        public int FolderSize { get; set; }
        // How many charts this folder's most-held end took.
        public int FolderCut { get; set; }
        public DateTimeOffset ComputedAt { get; set; }
    }
}
