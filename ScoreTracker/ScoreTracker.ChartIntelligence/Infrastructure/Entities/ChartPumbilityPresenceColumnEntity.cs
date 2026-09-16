using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // One PUMBILITY presence column on a mix (docs/design/chart-presence-graph.md §3): the title it
    // stands for and everyone standing on it. Rewritten with the presence rows.
    // Composite key configured in ChartIntelligenceModelContribution.
    internal class ChartPumbilityPresenceColumnEntity
    {
        public Guid MixId { get; set; }

        public int ColumnOrder { get; set; }

        [MaxLength(64)] public string Band { get; set; } = string.Empty;

        public int Players { get; set; }

        // The PIU Scores accounts among Players, the denominator for a chart with no official ranking.
        public int SitePlayers { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
