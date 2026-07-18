using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ScoreLedger.Infrastructure.Entities;

// Append-only journal of score submissions as received (ADR-001 Q8): the foundation of
// the score-progression-history feature, and the candidate source-of-truth if the
// Ledger is ever event-sourced. Rows are never updated or deleted.
[Index(nameof(UserId), nameof(ChartId), nameof(OccurredAt))]
[Index(nameof(UserId), nameof(MixId), nameof(OccurredAt))]
internal sealed class ScoreEventJournalEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid EventId { get; set; }

    [Required] public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Acquisition channel: manual | officialImport | csv | backfill.</summary>
    [Required]
    [MaxLength(32)]
    public string Source { get; set; } = string.Empty;

    [Required] public Guid MixId { get; set; }

    [Required] public Guid UserId { get; set; }

    [Required] public Guid ChartId { get; set; }

    public int? Score { get; set; }

    public string? Plate { get; set; }

    [Required] public bool IsBroken { get; set; }

    /// <summary>
    ///     Play-session / import-run grouping (Session Batcher). NULL = row predates
    ///     session capture; never backfilled.
    /// </summary>
    public Guid? SessionId { get; set; }

    /// <summary>
    ///     Judgement breakdown of the play behind this journal state. All five are set
    ///     together or not at all; NULL = not observed for this event.
    /// </summary>
    public int? Perfects { get; set; }

    public int? Greats { get; set; }
    public int? Goods { get; set; }
    public int? Bads { get; set; }
    public int? Misses { get; set; }
}
