using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ScoreLedger.Infrastructure.Entities;

// Append-only journal of plays as observed (ADR-001 Q8): the foundation of the
// score-progression-history feature, and the candidate source-of-truth if the Ledger is
// ever event-sourced. Rows are never updated or deleted — one row is one play, identified
// by (UserId, MixId, ChartId, OccurredAt), so re-importing a play collapses onto it
// instead of duplicating (docs/design/score-truth-model.md).
[Index(nameof(UserId), nameof(ChartId), nameof(OccurredAt))]
[Index(nameof(UserId), nameof(MixId), nameof(OccurredAt))]
[Index(nameof(UserId), nameof(MixId), nameof(ChartId), nameof(OccurredAt), IsUnique = true)]
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
    ///     Whether this play became the player's record when it was written. False for the
    ///     plays the official site's recently-played list reports that never beat a best.
    ///     Defaults to TRUE, in both the CLR and the column: every row written before the
    ///     observation path existed was a best, and only that one path ever writes false — so
    ///     an omitted value is right far more often than not, and a wrong "false" is invisible
    ///     (the play silently drops out of a chart's history as a mere replay).
    /// </summary>
    [Required]
    public bool IsBest { get; set; } = true;

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
