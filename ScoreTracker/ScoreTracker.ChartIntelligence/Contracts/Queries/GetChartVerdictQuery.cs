using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     A chart's fired verdict facets in salience order (headline first). Cached per
///     (chart, mix) until shortly after the nightly analytics chain rebuilds. Population
///     is always present; everything else only when its evidence bar is met.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartVerdictQuery(Guid ChartId, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IReadOnlyList<ChartVerdictFacet>>
{
}
