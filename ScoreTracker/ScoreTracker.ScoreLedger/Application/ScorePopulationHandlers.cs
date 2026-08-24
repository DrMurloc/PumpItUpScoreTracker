using MediatR;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     The score calculator's population census (docs/design/phoenix-score-calculator.md D9).
///     One grouped read, cached for hours — the section it feeds moves at the pace of the
///     whole population, not of any one import.
/// </summary>
internal sealed class GetScorePopulationHandler(IScorePopulationRepository repository, IMemoryCache cache)
    : IRequestHandler<GetScorePopulationQuery, IReadOnlyList<LevelScorePopulation>>
{
    public async Task<IReadOnlyList<LevelScorePopulation>> Handle(GetScorePopulationQuery request,
        CancellationToken cancellationToken)
    {
        return (await cache.GetOrCreateAsync(LedgerCacheKeys.ScorePopulation(request.Mix), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LedgerCacheKeys.ScorePopulationTtl;
            return await repository.GetPopulationByLevel(request.Mix, cancellationToken);
        }))!;
    }
}

/// <summary>
///     The measured per-grade judgement spreads (design doc D8). The repository hands back raw
///     judged bests; the grade resolves here from the queried mix's floor table, so a Phoenix 2
///     read bands its 800–950k stretch by its own letters. Per-1,000 figures are means of each
///     play's own per-1,000 mix, which weights every play equally regardless of chart length.
/// </summary>
internal sealed class GetJudgementSpreadsHandler(IScorePopulationRepository repository, IMemoryCache cache)
    : IRequestHandler<GetJudgementSpreadsQuery, IReadOnlyList<GradeJudgementSpread>>
{
    public async Task<IReadOnlyList<GradeJudgementSpread>> Handle(GetJudgementSpreadsQuery request,
        CancellationToken cancellationToken)
    {
        return (await cache.GetOrCreateAsync(LedgerCacheKeys.JudgementSpreads(request.Mix), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LedgerCacheKeys.JudgementSpreadsTtl;
            var judged = await repository.GetJudgedBests(request.Mix, cancellationToken);
            return Aggregate(judged, request.Mix);
        }))!;
    }

    private static IReadOnlyList<GradeJudgementSpread> Aggregate(IReadOnlyList<JudgedBest> judged, MixEnum mix)
    {
        return judged
            .Select(best => new
            {
                Best = best,
                Notes = best.Perfects + best.Greats + best.Goods + best.Bads + best.Misses
            })
            .Where(x => x.Notes > 0)
            .GroupBy(x => ((PhoenixScore)Math.Clamp(x.Best.Score, 0, 1_000_000)).LetterGradeFor(mix))
            .Select(band =>
            {
                var withCombo = band.Where(x => x.Best.MaxCombo != null).ToArray();
                return new GradeJudgementSpread(
                    band.Key,
                    band.Count(),
                    band.Average(x => x.Best.Perfects * 1000.0 / x.Notes),
                    band.Average(x => x.Best.Greats * 1000.0 / x.Notes),
                    band.Average(x => x.Best.Goods * 1000.0 / x.Notes),
                    band.Average(x => x.Best.Bads * 1000.0 / x.Notes),
                    band.Average(x => x.Best.Misses * 1000.0 / x.Notes),
                    withCombo.Length == 0
                        ? 0
                        : withCombo.Average(x => x.Best.MaxCombo!.Value * 1000.0 / x.Notes),
                    withCombo.Length);
            })
            .OrderByDescending(spread => (int)spread.Grade.GetMinimumScoreFor(mix))
            .ToArray();
    }
}

/// <summary>The plays dialog's list (design doc D7) — a straight repository pass-through.</summary>
internal sealed class GetJudgedPlaysHandler(IScoreJournalRepository journal)
    : IRequestHandler<GetJudgedPlaysQuery, IReadOnlyList<ScoreJournalEntry>>
{
    public async Task<IReadOnlyList<ScoreJournalEntry>> Handle(GetJudgedPlaysQuery request,
        CancellationToken cancellationToken)
    {
        return await journal.GetJudgedPlays(request.UserId, request.Mix,
            Math.Clamp(request.Limit, 1, 500), cancellationToken);
    }
}
