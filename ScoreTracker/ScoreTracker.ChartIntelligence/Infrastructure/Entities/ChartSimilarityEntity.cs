using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities;

// The similarity graph's persisted edges: the top-20 neighbors per (mix, chart), rebuilt
// wholesale by the nightly job and stored without a score bar so the shelf can move its
// own. SignalsJson keeps the per-signal breakdown and the shared badges the shelf names
// its matches from — a blob rather than columns because it is read whole, only ever by
// this vertical, and never queried into.
internal sealed class ChartSimilarityEntity
{
    [Required] public Guid MixId { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public Guid SimilarChartId { get; set; }

    [Required] public double Score { get; set; }

    [Required] public string SignalsJson { get; set; } = string.Empty;

    [Required] public DateTimeOffset ComputedAt { get; set; }
}
