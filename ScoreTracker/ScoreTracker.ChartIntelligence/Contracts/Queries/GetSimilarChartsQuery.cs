using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     A chart's similarity-graph neighbors, best match first (at most eight — the
///     nightly job's top-K). Empty for charts too sparse for any edge, for mixes the
///     job doesn't run on yet, and before the first rebuild.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSimilarChartsQuery(Guid ChartId, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IReadOnlyList<ChartSimilarityRecord>>
{
}
