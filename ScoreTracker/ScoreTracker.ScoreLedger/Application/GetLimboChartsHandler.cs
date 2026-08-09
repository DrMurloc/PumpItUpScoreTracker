using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Answers "does this chart carry a limbo leaderboard" from a five-minute cache. Every chart
///     view asks, and the answer is a two-column table nobody but a person at a SQL prompt writes.
/// </summary>
internal sealed class GetLimboChartsHandler(ILimboChartRepository charts, IMemoryCache cache)
    : IRequestHandler<GetLimboChartsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(GetLimboChartsQuery request,
        CancellationToken cancellationToken)
    {
        var key = LedgerCacheKeys.LimboCharts(request.Mix);
        if (cache.TryGetValue(key, out IReadOnlySet<Guid>? cached) && cached != null) return cached;

        var flagged = await charts.GetLimboCharts(request.Mix, cancellationToken);
        cache.Set(key, flagged, LedgerCacheKeys.LimboChartsTtl);
        return flagged;
    }
}
