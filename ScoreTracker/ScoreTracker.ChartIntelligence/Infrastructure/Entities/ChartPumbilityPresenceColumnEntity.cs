using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // One PUMBILITY presence column on a mix (docs/design/chart-presence-graph.md §3): the title it
    // stands for and everyone standing on it. Rewritten with the presence rows.
    // Composite key configured in ChartIntelligenceModelContribution.
    internal class ChartPumbilityPresenceColumnEntity
    {
        public Guid MixId { get; set; }

        // The layout: true for the columns counted over everyone, false for the columns counted over
        // PIU Scores accounts alone, which a chart with no official ranking reads.
        public bool CountsBoardPlayers { get; set; }

        public int ColumnOrder { get; set; }

        [MaxLength(64)] public string Band { get; set; } = string.Empty;

        public int Players { get; set; }

        // The PIU Scores accounts among Players.
        public int SitePlayers { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
