using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     The failure rail's rows for one chart (docs/design/step-chart-failure-map.md D2/D3).
///     The anonymous read caches per (mix, chart) — imports append all day and nothing needs
///     to see a death the minute it lands — while the viewer's-own flag is computed per
///     request, so the cache never carries a viewer's perspective.
/// </summary>
internal sealed class GetChartStageBreaksHandler(
    IScoreJournalRepository journal,
    IMemoryCache cache)
    : IRequestHandler<GetChartStageBreaksQuery, ChartStageBreaksRecord>
{
    public async Task<ChartStageBreaksRecord> Handle(GetChartStageBreaksQuery request,
        CancellationToken cancellationToken)
    {
        var read = await cache.GetOrCreateAsync(
            LedgerCacheKeys.StageBreaks(request.Mix, request.ChartId),
            entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = LedgerCacheKeys.StageBreaksTtl;
                return journal.GetChartStageBreaks(request.Mix, request.ChartId, cancellationToken);
            });

        return new ChartStageBreaksRecord(
            (read?.Rows ?? Array.Empty<ChartStageBreakRow>())
            .Select(r => new ChartStageBreakRecord(r.Judgements.NoteCount, r.IsNonLifebarBreak,
                request.ViewerId != null && r.UserId == request.ViewerId, r.PassPlate, r.PassGrade,
                r.Judgements.Misses))
            .ToArray(),
            read?.Unplaced ?? 0);
    }
}
