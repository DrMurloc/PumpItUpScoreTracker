using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(Level))]
[Index(nameof(Type))]
[Index(nameof(SongId))]
public sealed class ChartEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid SongId { get; set; }

    [Required] public int Level { get; set; }

    [Required] public string Type { get; set; } = string.Empty;
    [MaxLength(128)] public string? StepArtist { get; set; }
    [Required] public Guid OriginalMixId { get; set; }

    /// <summary>
    ///     Explicit player count — historically derived as CoOp-charts-use-Level, but
    ///     legacy Routine-era co-ops carry a real difficulty in Level AND a player
    ///     count, so the pun is retired (docs/design/legacy-mixes.md). Backfilled by
    ///     the LegacyChartSchema migration; 1 for every non-co-op chart.
    /// </summary>
    [Required]
    public int PlayerCount { get; set; } = 1;
}