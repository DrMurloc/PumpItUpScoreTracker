using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Recap;

/// <summary>
///     The persisted season-recap payload — computed by the recap saga, stored as JSON on
///     scores.PlayerSeasonRecap, read verbatim by the recap page. Sections are nullable
///     when a player lacks the data to fill them; the page skips empty sections.
///     Bump <see cref="CurrentSchemaVersion" /> on any breaking shape change — stored
///     payloads with an older version read as "no recap yet" until recomputed.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerRecap(
    int SchemaVersion,
    DateTimeOffset ComputedAt,
    RecapArc? Arc,
    RecapRollup Rollup,
    RecapPlayerType? PlayerType,
    int? PlayerTypeAverageScore,
    IReadOnlyList<RecapEarnedBadge> Badges,
    RecapRivals? Rivals,
    IReadOnlyList<RecapChartHighlight> ImpressivePgs,
    IReadOnlyList<RecapScoreHighlight> ImpressiveScores,
    IReadOnlyList<RecapRareChart> RarestPasses,
    RecapWeekly? Weekly,
    RecapTrophies Trophies,
    RecapPhoenix2Projection? Projection)
{
    public const int CurrentSchemaVersion = 2;
}

/// <summary>Competitive-level journey, first PlayerHistory snapshot to the latest.</summary>
[ExcludeFromCodeCoverage]
public sealed record RecapArc(
    double StartCompetitive,
    double EndCompetitive,
    double StartSingles,
    double EndSingles,
    double StartDoubles,
    double EndDoubles,
    int StartPassCount,
    int EndPassCount,
    IReadOnlyList<RecapArcPoint> Points);

[ExcludeFromCodeCoverage]
public sealed record RecapArcPoint(DateTimeOffset Date, double CompetitiveLevel);

[ExcludeFromCodeCoverage]
public sealed record RecapRollup(
    int PlayDays,
    DateTimeOffset? FirstRecordedOn,
    int ChartsPassed,
    int ChartsPassedRank,
    double ChartsPassedPercentile,
    int? SinglesRank,
    double? SinglesPercentile,
    int? DoublesRank,
    double? DoublesPercentile,
    long TotalDurationSeconds,
    long TotalNoteCount,
    IReadOnlyList<RecapStepArtist> TopStepArtists);

[ExcludeFromCodeCoverage]
public sealed record RecapStepArtist(string Name, int ChartCount);

/// <summary>
///     One earned badge with the number that earned it: Share for percentage ladders
///     (titles, co-op, BanYa), Count for folder ladders, Subject for badges about a
///     specific title or chart (Special Snowflake, Big Feet).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecapEarnedBadge(RecapBadge Badge, double? Share, int? Count, string? Subject);

[ExcludeFromCodeCoverage]
public sealed record RecapRivals(IReadOnlyList<RecapRival> Singles, IReadOnlyList<RecapRival> Doubles);

[ExcludeFromCodeCoverage]
public sealed record RecapRival(Guid UserId, string Name, double CompetitiveLevel, int SharedTop50Charts);

/// <summary>An impressive PG or pass: ordered by folder then tier-list difficulty.</summary>
[ExcludeFromCodeCoverage]
public sealed record RecapChartHighlight(
    Guid ChartId,
    string SongName,
    ChartType ChartType,
    int Level,
    TierListCategory Difficulty);

[ExcludeFromCodeCoverage]
public sealed record RecapScoreHighlight(
    Guid ChartId,
    string SongName,
    ChartType ChartType,
    int Level,
    int Score,
    double PercentileVsPeers);

/// <summary>A pass few others have: sitewide pass rate (passers ÷ players who tried it) and the passer count.</summary>
[ExcludeFromCodeCoverage]
public sealed record RecapRareChart(
    Guid ChartId,
    string SongName,
    ChartType ChartType,
    int Level,
    double PassRate,
    int Passers);

[ExcludeFromCodeCoverage]
public sealed record RecapWeekly(
    int LongestStreak,
    int WeeksEntered,
    int PodiumsInRange,
    int WinsInRange,
    int GiantSlayerCount,
    IReadOnlyList<RecapGiantSlayer> TopGiantSlayers,
    RecapBestWeek? BestWeek);

[ExcludeFromCodeCoverage]
public sealed record RecapGiantSlayer(
    Guid ChartId,
    string SongName,
    ChartType ChartType,
    int Level,
    string OutscoredName,
    double LevelGap);

[ExcludeFromCodeCoverage]
public sealed record RecapBestWeek(
    int Place,
    int OfCount,
    Guid ChartId,
    string SongName,
    ChartType ChartType,
    int Level,
    DateTimeOffset Week);

[ExcludeFromCodeCoverage]
public sealed record RecapTrophies(
    IReadOnlyList<RecapRareTitle> RarestTitles,
    IReadOnlyList<RecapPlateCount> PlateCounts,
    string? HighestTitle,
    int? HighestTitleHolders,
    IReadOnlyList<RecapGradeCount> GradeCounts,
    int SinglesPassCount,
    int DoublesPassCount);

[ExcludeFromCodeCoverage]
public sealed record RecapRareTitle(string Title, double HolderShare);

[ExcludeFromCodeCoverage]
public sealed record RecapPlateCount(PhoenixPlate Plate, int Count);

/// <summary>Pass counts per letter grade with plus-grades folded in (SS+ counts as SS).</summary>
[ExcludeFromCodeCoverage]
public sealed record RecapGradeCount(PhoenixLetterGrade Grade, int Count);

/// <summary>
///     Phoenix 1 scores rescored on Phoenix 2 levels with the P2 formula: two top-50
///     pools plus the highest [S]- and [D]-ladder pumbility titles those pools reach.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RecapPhoenix2Projection(
    int SinglesPumbility,
    int DoublesPumbility,
    int TotalPumbility,
    string? ProjectedSinglesTitle,
    string? ProjectedDoublesTitle,
    int CarriedOverPasses,
    int TotalPasses);
