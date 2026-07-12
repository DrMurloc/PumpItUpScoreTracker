using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    // Pass-count histograms per folder per competitive-level bucket (round 7: the
    // "Folder Passes vs Similar Players" strip bar), maintained by TierListSaga during
    // the daily scores rebuild. Bucket = competitive level × 2, rounded; the histogram
    // is a JSON map of passes → player count. Composite key configured in
    // ChartIntelligenceModelContribution.
    internal class FolderCohortStatsEntity
    {
        public Guid MixId { get; set; }

        [MaxLength(16)] public string ChartType { get; set; } = string.Empty;

        public int Level { get; set; }

        public int Bucket { get; set; }

        public string PassHistogramJson { get; set; } = string.Empty;
    }
}
