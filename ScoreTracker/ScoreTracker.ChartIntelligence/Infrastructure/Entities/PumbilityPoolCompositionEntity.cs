using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // Where PUMBILITY comes from across every full merged pool on a mix, one row per band of the
    // total (docs/design/pumbility-calculator.md D9). Sums, not averages, so the reader divides as it
    // needs; the grade histogram rides as JSON. Rewritten wholesale per mix by the nightly PUMBILITY
    // tier-list sweep. Composite key configured in ChartIntelligenceModelContribution.
    internal class PumbilityPoolCompositionEntity
    {
        public Guid MixId { get; set; }

        [MaxLength(64)] public string BandKey { get; set; } = string.Empty;

        [MaxLength(64)] public string? Title { get; set; }

        public double Floor { get; set; }

        public double? Ceiling { get; set; }

        public int Players { get; set; }

        public int ChartsPooled { get; set; }

        public double LevelSum { get; set; }

        public double LevelPart { get; set; }

        public double ScorePart { get; set; }

        public double PlatePart { get; set; }

        public string GradeCountsJson { get; set; } = string.Empty;

        // Mix-level facts carried on every row, so a read is one keyed range with no second table.
        public int PoolsCounted { get; set; }

        public DateTimeOffset ComputedAt { get; set; }
    }
}
