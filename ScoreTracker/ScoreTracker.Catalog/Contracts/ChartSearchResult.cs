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
///     The mix's real extents for the range facets, so a slider's travel matches the
///     catalogue instead of a guessed span. Null where nothing in the mix carries the value.
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
///     One chart in the searched mix, with its community and personal overlays. DebutMix is
///     the chart's own origin (it rides every appearance), which is what powers the
///     newest-content sort without needing a cross-mix scope.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSearchResult(
    Chart Chart,
    MixEnum DebutMix,
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

/// <summary>A granular piucenter badge with its English display name; the UI localizes DisplayName.</summary>
[ExcludeFromCodeCoverage]
public sealed record ChartBadge(string Key, string DisplayName, SkillCategory? Category);

/// <summary>
///     The signed-in visitor's best on this chart, family-shaped: Phoenix fields for
///     Phoenix-family mixes, Legacy fields for XX and older — never both.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSearchMyState(
    int? PhoenixScore,
    PhoenixLetterGrade? PhoenixGrade,
    PhoenixPlate? PhoenixPlate,
    XXLetterGrade? LegacyGrade,
    int? LegacyScore,
    bool IsBroken,
    bool Passed,
    DateTimeOffset? RecordedOn);
