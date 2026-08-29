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

    /// <summary>
    ///     The XX-and-older letter grade. Legacy has no plate: the letter IS the plate
    ///     equivalent, and it is the axis most legacy records carry — the numeric Score is
    ///     usually absent. It gets its own column rather than riding in Plate, because a
    ///     Phoenix reader parsing "SSS" as a PhoenixPlate throws, and storing one scoring
    ///     model's data in another's column is the defect this whole effort removed.
    /// </summary>
    public string? LetterGrade { get; set; }

    [Required] public bool IsBroken { get; set; }

    /// <summary>
    ///     A play the stage interrupted — the song ended before its last note. Always broken,
    ///     never best, never scored: the running number the Phoenix 2 best list prints for one
    ///     is normalised over the notes judged so far and is not a chart score. Defaults to
    ///     FALSE in the CLR and the column: every row written before the parser read the
    ///     site's STAGE BREAK label was a finished play, because the label was skipped outright
    ///     (docs/design/stage-breaks-and-max-combo.md D11).
    /// </summary>
    [Required]
    public bool IsStageBroken { get; set; }

    /// <summary>
    ///     The life bar provably could not have emptied on this run, so one of Phoenix 2's Stage
    ///     Pass commands ended it. Defaults to FALSE in the CLR and the column: a row we have not
    ///     classified must never read as a positive claim, and the great majority of stage breaks
    ///     really are the bar running out (docs/design/pass-command-detection.md D29).
    /// </summary>
    [Required]
    public bool IsNonLifebarBreak { get; set; }

    /// <summary>
    ///     The plate the run's last judgement put out of reach, by the plate's full name, or NULL
    ///     when no plate broke by exactly one judgement. Independent of <see cref="PassGrade" /> —
    ///     either, both or neither may be set on a non-lifebar break, and both empty is a real
    ///     answer rather than a gap (D31, D32).
    /// </summary>
    [MaxLength(16)]
    public string? PassPlate { get; set; }

    /// <summary>
    ///     The letter grade the run's last judgement put out of reach, or NULL. Derived from the
    ///     best score still attainable at the break, so it moves with the chart's note count and
    ///     the mix's own floors (D33).
    /// </summary>
    [MaxLength(8)]
    public string? PassGrade { get; set; }

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

    /// <summary>
    ///     The max combo solved from the breakdown above and the score at write time
    ///     (PhoenixComboSolver). NULL when there is no breakdown, the chart's note count is
    ///     unknown, the breakdown falls short of it, or the play is a stage break — and
    ///     re-derived wholesale by the Backfill max combos admin button.
    /// </summary>
    public int? MaxCombo { get; set; }
}
