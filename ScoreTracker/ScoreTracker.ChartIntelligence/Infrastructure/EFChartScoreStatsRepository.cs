using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFChartScoreStatsRepository : IChartScoreStatsRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartScoreStatsRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveStats(MixEnum mix, IEnumerable<ChartScoreStatsRecord> stats,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var statArray = stats.ToArray();
        var chartIds = statArray.Select(s => s.ChartId).ToArray();
        var existing = (await database.Set<ChartScoreStatsEntity>()
                .Where(e => e.MixId == mixId && chartIds.Contains(e.ChartId))
                .ToArrayAsync(cancellationToken))
            .ToDictionary(e => e.ChartId);

        foreach (var stat in statArray)
            if (existing.TryGetValue(stat.ChartId, out var entity))
            {
                entity.ScoreStandardDeviation = stat.ScoreStandardDeviation;
                entity.ScoreCount = stat.ScoreCount;
            }
            else
            {
                await database.Set<ChartScoreStatsEntity>().AddAsync(new ChartScoreStatsEntity
                {
                    MixId = mixId,
                    ChartId = stat.ChartId,
                    ScoreStandardDeviation = stat.ScoreStandardDeviation,
                    ScoreCount = stat.ScoreCount
                }, cancellationToken);
            }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ChartScoreStatsRecord>> GetStats(MixEnum mix, IEnumerable<Guid> chartIds,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var ids = chartIds.Distinct().ToArray();
        return (await database.Set<ChartScoreStatsEntity>()
                .Where(e => e.MixId == mixId && ids.Contains(e.ChartId))
                .ToArrayAsync(cancellationToken))
            .Select(e => new ChartScoreStatsRecord(e.ChartId, e.ScoreStandardDeviation, e.ScoreCount));
    }
}
