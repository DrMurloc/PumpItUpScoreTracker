namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // The Hardmode chart list: the rarest slice of every folder on a mix, one row per chart
    // (docs/design/hardmode-leaderboard.md §1). Rewritten wholesale by the weekly census — a
    // partial rewrite would leave last week's charts qualifying beside this week's.
    // No UserId: this is a chart-level fact about a population, not about anyone.
    // Composite key configured in ChartIntelligenceModelContribution.
    internal class HardmodeChartEntity
    {
        public Guid MixId { get; set; }

        public Guid ChartId { get; set; }

        public int Level { get; set; }

        // The weighted hold count, 51 − slot summed over every full pool that holds it. Float
        // because it is a sum that surfaces divide, and rounding at rest costs precision the UI
        // may want to spend — the same reason PlayerStats keeps its pools unrounded.
        public double Points { get; set; }

        // Distinct players holding it anywhere, so three pools of one player count once.
        public int Holders { get; set; }

        public int FolderSize { get; set; }

        public int FolderCut { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
