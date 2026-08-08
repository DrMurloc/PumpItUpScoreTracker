namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The per-flag detail a highlighted score carries beyond its flag bits — computed at
///     capture and persisted so it stays historically true. The chart's pumbility rank, its
///     folder-debut ordinal, the peer-cohort standing (total · how many scored higher · how
///     many hold the PG), and the chart-specific skill title it progressed (name · current
///     score · threshold). Every field is null when its flag isn't present or needs no detail.
///     <para>
///         <see cref="PeerPercentile" /> is the exception: it rides every captured score, not
///         only the ones that flagged, because the Sessions page colours every row by it
///         (docs/design/session-breakdown.md §2.2). Null means the chart had no cohort — co-op,
///         or more than five levels below the player's competitive level.
///     </para>
///     <para>
///         ⚠ This is a POSITIONAL record. New members append; inserting one silently shifts
///         every argument after it at each of the several sites that construct it by position.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HighlightDetail(
    int? PumbilityRank = null,
    int? FolderDebutOrdinal = null,
    int? PeerCount = null,
    int? PeerBetterCount = null,
    int? PeerPgCount = null,
    string? SkillTitleName = null,
    int? SkillTitleScore = null,
    int? SkillTitleThreshold = null,
    double? PeerPercentile = null,
    int? AttemptsBeforeClear = null,
    int? OfficialPlace = null,
    int? OfficialBoardDepth = null,
    DateTimeOffset? OfficialAsOf = null,
    double? CompetitiveBaseline = null,
    int? PumbilityGain = null);
