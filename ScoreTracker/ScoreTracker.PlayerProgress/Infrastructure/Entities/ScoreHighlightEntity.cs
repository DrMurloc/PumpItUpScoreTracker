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
}
