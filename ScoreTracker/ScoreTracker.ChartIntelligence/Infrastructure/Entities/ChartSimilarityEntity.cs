using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities;

// The similarity graph's persisted edges: top-K neighbors per (mix, chart), rebuilt
// wholesale by the nightly job. SignalsJson keeps the per-signal breakdown the shelf names
// its matches from. SharedScorers is dead weight — nothing writes it since the collaborative
// signal left the formula, so every row carries a 0.
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
