using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     A chart's similarity-graph neighbors, best match first (at most twenty — the
///     nightly job's top-K). Empty for charts the piucenter crawl never covered, for
///     mixes the job doesn't run on yet, and before the first rebuild.
///     The list runs down into the tail: it is **not** filtered to real matches, because
///     where that bar falls is a render decision and the rows below it are what the
///     near-misses section shows. Apply your own.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSimilarChartsQuery(Guid ChartId, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IReadOnlyList<ChartSimilarityRecord>>
{
}
