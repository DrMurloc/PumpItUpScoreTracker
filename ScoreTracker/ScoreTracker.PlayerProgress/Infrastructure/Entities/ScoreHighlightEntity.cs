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

    /// <summary>Bit flags — see <c>HighlightFlags</c> in PlayerProgress contracts.</summary>
    [Required]
    public int Flags { get; set; }

    /// <summary>Denormalized for the universal noteworthy ordering (level desc, scoring level desc).</summary>
    [Required]
    public int Level { get; set; }

    public double? ScoringLevel { get; set; }

    // Caption detail computed at capture (historically true) — see HighlightDetail in
    // PlayerProgress contracts. Null when the row's flags need no detail.
    public int? PumbilityRank { get; set; }

    public int? FolderDebutOrdinal { get; set; }

    public int? PeerCount { get; set; }

    public int? PeerBetterCount { get; set; }

    public int? PeerPgCount { get; set; }

    public string? SkillTitleName { get; set; }

    public int? SkillTitleScore { get; set; }

    public int? SkillTitleThreshold { get; set; }

    /// <summary>
    ///     Tie-inclusive percentile against the competitive cohort. Set on every captured
    ///     score, not only flagged ones — the Sessions page colours every row by it. Null
    ///     means no cohort existed (co-op, or below the competitive − 5 gate).
    /// </summary>
    public double? PeerPercentile { get; set; }

    /// <summary>Plays of this chart in the same session before the one that cleared it.</summary>
    public int? AttemptsBeforeClear { get; set; }

    public int? OfficialPlace { get; set; }

    public int? OfficialBoardDepth { get; set; }

    /// <summary>The sealed snapshot the placement was estimated against.</summary>
    public DateTimeOffset? OfficialAsOf { get; set; }

    /// <summary>
    ///     The player's competitive level for THIS chart's type, as it stood when the batch
    ///     opened — the number the CompetitiveImprover flag compared the score against. Stored
    ///     because it is per-batch and unrecoverable later: a session drains as several batches,
    ///     each with its own before and after, and the stats row only remembers the last one.
    ///     The score's own competitive level stays a pure function of level and score, so one
    ///     column buys both halves of "23.6 (+0.4)".
    /// </summary>
    public double? CompetitiveBaseline { get; set; }

    /// <summary>
    ///     What this play added to the combined PUMBILITY pool. Null when it added nothing, or
    ///     when the play predates the capture. Phoenix 2's Singles and Doubles pools are
    ///     deliberately not measured here — the row reports one number, and the combined pool is
    ///     the one the ceremony band headlines.
    /// </summary>
    public double? PumbilityGain { get; set; }
}
