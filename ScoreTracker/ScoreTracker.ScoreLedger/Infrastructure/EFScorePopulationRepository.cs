using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFScorePopulationRepository : IScorePopulationRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFScorePopulationRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<LevelScorePopulation>> GetPopulationByLevel(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // The level is the mix's own — the same chart id carries a different level per mix, so
        // the record's row joins the ChartMix row of the record's mix, never the chart's base
        // level. Chart and ChartMix are shared unextracted tables on the one context.
        var rows = await (
                from record in database.Set<PhoenixRecordEntity>()
                join chartMix in database.ChartMix
                    on new { record.ChartId, record.MixId } equals new { chartMix.ChartId, chartMix.MixId }
                join chart in database.Chart on record.ChartId equals chart.Id
                where record.MixId == mixId && !record.IsBroken && record.Score != null
                      && (chart.Type == nameof(ChartType.Single) || chart.Type == nameof(ChartType.Double))
                group record by chartMix.Level
                into byLevel
                select new
                {
                    Level = byLevel.Key,
                    Total = byLevel.Count(),
                    Below900k = byLevel.Sum(r => r.Score < 900_000 ? 1 : 0),
                    From900k = byLevel.Sum(r => r.Score >= 900_000 && r.Score < 950_000 ? 1 : 0),
                    From950k = byLevel.Sum(r => r.Score >= 950_000 && r.Score < 970_000 ? 1 : 0),
                    From970k = byLevel.Sum(r => r.Score >= 970_000 && r.Score < 980_000 ? 1 : 0),
                    From980k = byLevel.Sum(r => r.Score >= 980_000 && r.Score < 990_000 ? 1 : 0),
                    From990k = byLevel.Sum(r => r.Score >= 990_000 && r.Score < 995_000 ? 1 : 0),
                    From995k = byLevel.Sum(r => r.Score >= 995_000 ? 1 : 0)
                })
            .ToArrayAsync(cancellationToken);
        return rows
            .OrderBy(r => r.Level)
            .Select(r => new LevelScorePopulation(r.Level, r.Total, r.Below900k, r.From900k, r.From950k,
                r.From970k, r.From980k, r.From990k, r.From995k))
            .ToArray();
    }

    public async Task<IReadOnlyList<JudgedBest>> GetJudgedBests(MixEnum mix,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Raw judged rows, seven ints each — tens of thousands today, aggregated by the
        // handler because grading needs the mix's floor table. Revisit the shape if the
        // judged share of the table ever makes this read heavy.
        return await database.Set<PhoenixRecordEntity>()
            .Where(r => r.MixId == mixId && !r.IsBroken && r.Score != null && r.Perfects != null)
            .Select(r => new JudgedBest(r.Score!.Value, r.Perfects!.Value, r.Greats!.Value, r.Goods!.Value,
                r.Bads!.Value, r.Misses!.Value, r.MaxCombo))
            .ToArrayAsync(cancellationToken);
    }
}
