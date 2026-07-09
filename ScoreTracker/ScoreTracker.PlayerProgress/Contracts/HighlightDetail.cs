namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The per-flag detail a highlighted score carries beyond its flag bits — computed at
///     capture and persisted so it stays historically true. The chart's pumbility rank, its
///     folder-debut ordinal, the peer-cohort standing (total · how many scored higher · how
///     many hold the PG), and the chart-specific skill title it progressed (name · current
///     score · threshold). Every field is null when its flag isn't present or needs no detail.
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
    int? SkillTitleThreshold = null);
