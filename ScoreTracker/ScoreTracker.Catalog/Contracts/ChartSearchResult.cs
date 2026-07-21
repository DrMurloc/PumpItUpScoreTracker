using System.Diagnostics.CodeAnalysis;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Catalog.Contracts;

[ExcludeFromCodeCoverage]
public sealed record ChartSearchResultPage(IReadOnlyList<ChartSearchResult> Results, int TotalCount,
    ChartSearchFacetCounts? FacetCounts = null);

/// <summary>
///     Result-set distributions for the enum facets (the drawer's count annotations):
///     how the CURRENT filtered set breaks down per value. Free-text facets never get
///     counts — that is where count cost hides.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSearchFacetCounts(
    IReadOnlyDictionary<ChartType, int> Types,
    IReadOnlyDictionary<SongType, int> SongTypes,
    IReadOnlyDictionary<string, int> Badges,
    IReadOnlyDictionary<TierListCategory, int> PassDifficulty,
    IReadOnlyDictionary<TierListCategory, int> ScoreDifficulty,
    IReadOnlyDictionary<TierListCategory, int> CommunityVote,
    IReadOnlyDictionary<LegacySlot, int> LegacySlots,
    IReadOnlyDictionary<MixEnum, int> DebutMixes,
    IReadOnlyDictionary<ChartScoreStateFilter, int> ScoreStates,
    IReadOnlyDictionary<int, int> CoOpPlayerCounts);

/// <summary>
///     The scope's real extents for the range facets, so a slider's travel matches the
///     catalogue instead of a guessed span. Null where nothing in scope carries the value.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSearchRanges(
    int? BpmMin, int? BpmMax,
    decimal? NpsMin, decimal? NpsMax,
    int? NoteCountMin, int? NoteCountMax,
    int? DurationSecondsMin, int? DurationSecondsMax,
    double? ScoringLevelMin, double? ScoringLevelMax);

/// <summary>
///     ScoringLevel is the difficulty-signal sort: score-derived scoring level for
///     Phoenix-family results, Community Vote average for XX-and-older results — vote data
///     never orders modern results, tiebreaks included.
/// </summary>
public enum ChartSearchSort
{
    Level,
    ScoringLevel,
    Popularity,
    PassRate,
    DebutEra,
    Name,
    Bpm,
    Nps,
    Duration,
    MyGrade,
    MyRecent
}

public enum ChartScoreStateFilter
{
    Unplayed,
    Played,
    Passed,
    Failed
}

/// <summary>
///     One chart identity across the searched scope. <see cref="Chart" /> is the linked
///     appearance (the visitor's mix when present, else the latest); appearances are
///     chronological. LevelChange is the signed Exceed-onward rerate delta (null when
///     fewer than two comparable appearances, or co-op).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSearchResult(
    Chart Chart,
    IReadOnlyList<ChartMixAppearance> Appearances,
    MixEnum DebutMix,
    MixEnum LatestMix,
    int? LevelChange,
    IReadOnlyList<ChartBadge> Badges,
    decimal? Nps,
    TierListCategory? PassDifficulty,
    TierListCategory? ScoreDifficulty,
    TierListCategory? CommunityVote,
    double? ScoringLevel,
    double? CommunityVoteRating,
    int ScoreCount,
    int PassCount,
    int PgCount,
    ChartSearchMyState? My);

[ExcludeFromCodeCoverage]
public sealed record ChartMixAppearance(MixEnum Mix, int Level, LegacySlot? Slot);

/// <summary>A granular piucenter badge with its English display name; the UI localizes DisplayName.</summary>
[ExcludeFromCodeCoverage]
public sealed record ChartBadge(string Key, string DisplayName, SkillCategory? Category);

/// <summary>
///     The signed-in visitor's best on the linked appearance, family-shaped: Phoenix fields
///     for Phoenix-family mixes, Legacy fields for XX and older — never both.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSearchMyState(
    int? PhoenixScore,
    PhoenixLetterGrade? PhoenixGrade,
    PhoenixPlate? PhoenixPlate,
    XXLetterGrade? LegacyGrade,
    int? LegacyScore,
    bool IsBroken,
    DateTimeOffset? RecordedOn,
    bool PassedInLinkedMix,
    bool PassedInAnotherScopeMix);
