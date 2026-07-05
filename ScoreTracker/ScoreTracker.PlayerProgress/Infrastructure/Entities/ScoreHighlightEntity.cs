using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities;

/// <summary>
///     Write-time notability capture for journal rows: flags are computed when the score
///     lands so they stay historically true (a read-time crown would drift as the top 50
///     moves). Joined to the journal by (SessionId, ChartId). Never backfilled.
/// </summary>
[Index(nameof(UserId), nameof(MixId), nameof(OccurredAt))]
internal sealed class ScoreHighlightEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid UserId { get; set; }

    [Required] public Guid MixId { get; set; }

    [Required] public Guid ChartId { get; set; }

    public Guid? SessionId { get; set; }

    [Required] public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Bit flags — see <c>HighlightFlag</c> in PlayerProgress contracts.</summary>
    [Required]
    public int Flags { get; set; }

    /// <summary>Denormalized for the universal noteworthy ordering (level desc, scoring level desc).</summary>
    [Required]
    public int Level { get; set; }

    public double? ScoringLevel { get; set; }
}
