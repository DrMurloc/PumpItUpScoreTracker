using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     Bulk sibling of <see cref="GetPlayerStatsQuery" /> — one read for a whole
///     population (the chart page buckets a chart's scorers by competitive level).
///     Users without stats in the mix are absent from the result.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayersStatsQuery(IEnumerable<Guid> UserIds, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<IEnumerable<PlayerStatsRecord>>
{
}
