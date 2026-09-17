using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // One chart on one PUMBILITY presence column (docs/design/chart-presence-graph.md §7): its holders'
    // spots and the rest of its folder's, on the title the column stands for. Written only where the
    // chart or its folder is held there. Rewritten wholesale per mix by the daily census.
    // No UserId: a chart-level fact about a population, not about anyone.
    // Composite key configured in ChartIntelligenceModelContribution.
    internal class ChartPumbilityPresenceEntity
    {
        public Guid MixId { get; set; }

        public Guid ChartId { get; set; }

        public int ColumnOrder { get; set; }

        // False for a chart piugame publishes no ranking for: its share counts PIU Scores accounts only.
        public bool CountsBoardPlayers { get; set; }

        public int Holders { get; set; }

        // Spots in a top 50, #1 the chart worth the most; null when nobody on the title holds the chart.
        public double? SpotMin { get; set; }

        public double? SpotP25 { get; set; }

        public double? SpotMedian { get; set; }

        public double? SpotP75 { get; set; }

        public double? SpotMax { get; set; }

        // Each holder's spot, invariant-culture and semicolon-separated, when there are too few for a box.
        [MaxLength(64)] public string? Dots { get; set; }

        public int FolderSpots { get; set; }

        public double? FolderP25 { get; set; }

        public double? FolderMedian { get; set; }

        public double? FolderP75 { get; set; }
    }
}
