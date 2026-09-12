using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFHardmodeChartRepository : IHardmodeChartRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFHardmodeChartRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Replace(MixEnum mix, IReadOnlyCollection<HardmodeChartRecord> charts,
        DateTimeOffset computedAt, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var existing = await database.Set<HardmodeChartEntity>().Where(e => e.MixId == mixId)
            .ToArrayAsync(cancellationToken);
        database.Set<HardmodeChartEntity>().RemoveRange(existing);
        await database.Set<HardmodeChartEntity>().AddRangeAsync(charts.Select(c => new HardmodeChartEntity
        {
            MixId = mixId,
            ChartId = c.ChartId,
            Level = c.Level,
            Points = c.Points,
            Holders = c.Holders,
            FolderSize = c.FolderSize,
            FolderCut = c.FolderCut,
            ComputedAt = computedAt
        }), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     The chart's type comes from the chart, not the row — the list is keyed by chart and the
    ///     type is already a fact about it, so storing a second copy would be a thing to drift.
    /// </summary>
    public async Task<IReadOnlyList<HardmodeChartRecord>> Get(MixEnum mix, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return await (from row in database.Set<HardmodeChartEntity>().Where(e => e.MixId == mixId)
                join chart in database.Set<ChartEntity>() on row.ChartId equals chart.Id
                select new HardmodeChartRecord(row.ChartId, Enum.Parse<ChartType>(chart.Type), row.Level,
                    row.Points, row.Holders, row.FolderSize, row.FolderCut))
            .ToArrayAsync(cancellationToken);
    }
}
