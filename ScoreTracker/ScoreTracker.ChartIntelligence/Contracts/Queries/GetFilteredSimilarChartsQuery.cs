using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     Similar charts computed live against a filtered target list
///     (docs/design/chart-similarity.md §5). Filters reduce **what we compare against** and
///     the scores are then recomputed — they never sieve the precalculated top-20, which
///     would trivially return nothing: those twenty are the nearest charts overall, so any
///     filter narrow enough to be interesting excludes all of them.
///     This is also the out-of-window path: "I liked this D18, what D23s play like it" is a
///     real question and deliberately outside the ±2 the nightly job precalculates. Pass
///     the level range you actually want; it is not clamped to the anchor's neighbourhood.
///     Every filter is null-means-unrestricted. Levels are inclusive. <see cref="DebutFrom" />
///     means "charts that debuted in this mix or later".
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetFilteredSimilarChartsQuery(
    Guid ChartId,
    MixEnum Mix = MixEnum.Phoenix,
    Name? StepArtist = null,
    decimal? MinBpm = null,
    decimal? MaxBpm = null,
    int? MinLevel = null,
    int? MaxLevel = null,
    MixEnum? DebutFrom = null) : IQuery<FilteredSimilarChartsRecord>;
