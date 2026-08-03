namespace ScoreTracker.Catalog.Contracts;

/// <summary>
///     PIU Center's step analysis for one chart, structured. Storage is a flat key-value bag
///     (<c>badge_fraction:bracket = 0.3333</c>, <c>top3:run = 1</c>, …) across 140-odd metric names;
///     this is that bag assembled into something a consumer can read without knowing the naming
///     scheme.
///     <para>
///         Not mix-scoped, and deliberately so: the analysis describes the steps, which do not change
///         when a chart's listed level does. Every other catalog read takes a mix; this one cannot.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSkillProfile(
    Guid ChartId,
    int? DataVersion,
    /// <summary>
    ///     Notes per second as PIU Center measures it. Deliberately NOT note count divided by
    ///     duration — that is a different number, and a consumer who recomputes it will disagree
    ///     with every other consumer.
    /// </summary>
    double? Nps,
    double? DifficultyPrediction,
    double? SustainTimeSeconds,
    double? TimeUnderTensionSeconds,
    bool? LastSegmentIsPeak,
    IReadOnlyList<ChartSkillCoverage> Skills,
    IReadOnlyList<ChartRarePattern> RarePatterns);

/// <summary>
///     One skill's presence in a chart, joined across the four per-skill metric families.
///     <see cref="Name" /> is piucenter's raw badge name (<c>twist_over90</c>, <c>bracket_jump</c>),
///     never the display skill vocabulary — that projection is lossy and naming these for a human is
///     the reader's job.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ChartSkillCoverage(
    string Name,
    double? Fraction,
    int? Top3Rank,
    int? PracticeRank,
    bool InLastSegment);

/// <summary>A pattern rare enough that piucenter counts occurrences rather than coverage.</summary>
[ExcludeFromCodeCoverage]
public sealed record ChartRarePattern(string Name, int Count);
