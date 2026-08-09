using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     The limbo board for one chart (docs/design/limbo-leaderboard.md). Answers empty for a chart
///     nobody flagged, rather than quietly serving a board that has no business existing — the
///     query is public, so the gate belongs here and not only in the component that draws it.
/// </summary>
internal sealed class GetLowestPassingScoresHandler(
    IScoreJournalRepository journal,
    IMediator mediator,
    IMemoryCache cache)
    : IRequestHandler<GetLowestPassingScoresQuery, IEnumerable<UserPhoenixScore>>
{
    public async Task<IEnumerable<UserPhoenixScore>> Handle(GetLowestPassingScoresQuery request,
        CancellationToken cancellationToken)
    {
        var flagged = await mediator.Send(new GetLimboChartsQuery(request.Mix), cancellationToken);
        if (!flagged.Contains(request.ChartId)) return Array.Empty<UserPhoenixScore>();

        var key = LedgerCacheKeys.LimboBoard(request.Mix, request.ChartId);
        if (cache.TryGetValue(key, out IReadOnlyList<UserPhoenixScore>? cached) && cached != null)
            return cached;

        var board = await journal.GetLowestPassingPlays(request.Mix, request.ChartId, request.Limit,
            cancellationToken);
        cache.Set(key, board, LedgerCacheKeys.LimboBoardTtl);
        return board;
    }
}
