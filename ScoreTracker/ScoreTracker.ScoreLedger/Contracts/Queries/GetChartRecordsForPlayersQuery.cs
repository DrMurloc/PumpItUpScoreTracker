using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     The best of each named player on one chart in a mix, with source and judgements — the
///     read behind <c>api/v2/charts/{chartId}/scores</c>. Players without a record on the chart
///     are absent; players outside <paramref name="UserIds" /> are never returned, which is what
///     lets a caller hand in exactly the accounts its credential may see.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartRecordsForPlayersQuery(MixEnum Mix, Guid ChartId, IReadOnlyCollection<Guid> UserIds)
    : IQuery<IReadOnlyList<PlayerChartRecord>>;
