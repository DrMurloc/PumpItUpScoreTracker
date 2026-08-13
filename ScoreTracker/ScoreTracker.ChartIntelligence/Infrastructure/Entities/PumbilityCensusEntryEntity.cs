using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // How many players in a cohort hold this chart in their top-50 PUMBILITY pool, plus the
    // tier that count banded into (docs/design/pumbility-tier-list.md). Written nightly by
    // PumbilityCensusSaga, one row set per folder per cohort.
    //
    // This does not live in TierListEntry because the cards print the peer count, and that
    // table has nowhere to put it. Folder (type, level) is denormalised so a read never joins
    // the catalog — and because a chart's level is per-mix, so ChartId alone does not imply it.
    //
    // CohortKey is the player grouping the count was taken over, resolved per mix: a Phoenix 1
    // difficulty title level, a Phoenix 2 PUMBILITY title rung, or Community for everyone at
    // once. Composite key configured in ChartIntelligenceModelContribution.
    internal class PumbilityCensusEntryEntity
    {
        /// <summary>The CohortKey standing for every player, which is the community view.</summary>
        internal const string CommunityCohort = "*";

        public Guid MixId { get; set; }

        [MaxLength(16)] public string ChartType { get; set; } = string.Empty;

        public int Level { get; set; }

        [MaxLength(64)] public string CohortKey { get; set; } = string.Empty;

        public Guid ChartId { get; set; }

        /// <summary>Players in the cohort whose pool contains this chart. Zero is a real answer.</summary>
        public int Appearances { get; set; }

        /// <summary>
        ///     Players in the cohort at all, which is what "ranked against N players" reads.
        ///     Constant across a cohort's rows and stored on each so the page never needs a
        ///     second read to say who it ranked you against.
        /// </summary>
        public int CohortSize { get; set; }

        [MaxLength(32)] public string Category { get; set; } = string.Empty;

        public int Order { get; set; }
    }
}
