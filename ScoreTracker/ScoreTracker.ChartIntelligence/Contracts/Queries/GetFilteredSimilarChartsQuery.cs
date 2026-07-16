using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     Similar charts computed live against a filtered target list
///     (docs/design/chart-similarity.md §5). Filters reduce **what we compare against** and
///     the scores are then recomputed — they never sieve the precalculated top-20, which
///     would trivially return nothing: those twenty are the nearest charts overall, so any
///     filter narrow enough to be interesting excludes all of them.
///     This is also the out-of-window path: "I liked this D18, what D23s play like it" is a
///     real question and deliberately outside the ±1 the nightly job precalculates. Pass
///     the level range you actually want; it is not clamped to the anchor's neighbourhood.
///     Four dimensions, and they are the whole set: two the chart declares (folder, BPM) and
///     two measured from it (scoring level, NPS). Each is a range rather than a value, and
///     each is something a reader can already see on the page — step artist and debut mix
///     lived here once and were neither.
///     Every filter is null-means-unrestricted, and every range is inclusive. A chart whose
///     scoring level was never measured filters at its listed level, matching what the rest
///     of the app reports for it; a chart with no NPS cannot answer an NPS filter, so one
///     excludes it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetFilteredSimilarChartsQuery(
    Guid ChartId,
    MixEnum Mix = MixEnum.Phoenix,
    int? MinLevel = null,
    int? MaxLevel = null,
    double? MinScoringLevel = null,
    double? MaxScoringLevel = null,
    decimal? MinBpm = null,
    decimal? MaxBpm = null,
    double? MinNps = null,
    double? MaxNps = null) : IQuery<FilteredSimilarChartsRecord>;
