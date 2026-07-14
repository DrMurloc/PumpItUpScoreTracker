using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities;

// The similarity graph's persisted edges (chart-details overhaul B1): top-K neighbors per
// (mix, chart), rebuilt wholesale by the nightly job. SignalsJson keeps the per-signal
// breakdown the similar-shelf why-chips render from; SharedScorers is surfaced separately
// because the S_players confidence floor (n ≥ 30) is a product rule, not a display detail.
internal sealed class ChartSimilarityEntity
{
    [Required] public Guid MixId { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public Guid SimilarChartId { get; set; }

    [Required] public double Score { get; set; }

    [Required] public string SignalsJson { get; set; } = string.Empty;

    [Required] public int SharedScorers { get; set; }

    [Required] public DateTimeOffset ComputedAt { get; set; }
}
