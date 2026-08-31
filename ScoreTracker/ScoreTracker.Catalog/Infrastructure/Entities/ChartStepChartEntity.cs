using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Catalog.Infrastructure.Entities;

/// <summary>
///     One chart's renderable step timeline (docs/design/step-chart-failure-map.md §3): the
///     gzip JSON payload StepChartPayloadCodec writes — rows/holds/ticks with limbs, beats
///     where the .ssc aligned, segments and the per-mix verdicts — plus the snapshot vintage it
///     was enriched from. Written only by the snapshot ingest and the reprocess consumer; read
///     per chart behind a memory cache.
/// </summary>
internal sealed class ChartStepChartEntity
{
    [Key] public Guid ChartId { get; set; }

    [Required] [MaxLength(32)] public string Vintage { get; set; } = string.Empty;

    [Required] public DateTimeOffset UpdatedAt { get; set; }

    [Required] public byte[] Payload { get; set; } = Array.Empty<byte>();
}
