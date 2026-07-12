using Microsoft.EntityFrameworkCore;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Catalog.Infrastructure;

internal sealed class EFChartSkillMetricRepository : IChartSkillMetricRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartSkillMetricRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task ReplaceChartMetrics(Guid chartId, string source, IEnumerable<ChartSkillMetric> metrics,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Set<ChartSkillMetricEntity>()
            .Where(e => e.ChartId == chartId && e.Source == source)
            .ToArrayAsync(cancellationToken);
        database.Set<ChartSkillMetricEntity>().RemoveRange(existing);
        await database.Set<ChartSkillMetricEntity>().AddRangeAsync(metrics.Select(m => new ChartSkillMetricEntity
        {
            ChartId = chartId,
            Source = source,
            MetricName = m.MetricName,
            Value = m.Value,
            Grade = m.Grade
        }), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChartSkillMetric>> GetMetrics(IEnumerable<Guid> chartIds, string source,
        CancellationToken cancellationToken = default)
    {
        var ids = chartIds.Distinct().ToArray();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ChartSkillMetricEntity>()
                .Where(e => e.Source == source && ids.Contains(e.ChartId))
                .ToArrayAsync(cancellationToken))
            .Select(e => new ChartSkillMetric(e.ChartId, e.MetricName, e.Value, e.Grade))
            .ToArray();
    }

    public async Task<IReadOnlySet<Guid>> GetChartIdsWithMetrics(string source,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ChartSkillMetricEntity>()
                .Where(e => e.Source == source)
                .Select(e => e.ChartId)
                .Distinct()
                .ToArrayAsync(cancellationToken))
            .ToHashSet();
    }
}
