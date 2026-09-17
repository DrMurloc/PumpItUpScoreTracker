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
        IReadOnlyCollection<HardmodeChartRecord> mostHeld, DateTimeOffset computedAt,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        // Both reads go through the AllTime filter, so a season's copy of either list is invisible
        // here and survives the rewrite (docs/design/seasons.md D31).
        database.Set<HardmodeChartEntity>().RemoveRange(await database.Set<HardmodeChartEntity>()
            .Where(e => e.MixId == mixId).ToArrayAsync(cancellationToken));
        database.Set<MostHeldChartEntity>().RemoveRange(await database.Set<MostHeldChartEntity>()
            .Where(e => e.MixId == mixId).ToArrayAsync(cancellationToken));
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
        await database.Set<MostHeldChartEntity>().AddRangeAsync(mostHeld.Select(c => new MostHeldChartEntity
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
        // One save for both lists: a glow must never pair this week's red with last week's green.
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

    public async Task<IReadOnlyList<HardmodeChartRecord>> GetMostHeld(MixEnum mix,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return await (from row in database.Set<MostHeldChartEntity>().Where(e => e.MixId == mixId)
                join chart in database.Set<ChartEntity>() on row.ChartId equals chart.Id
                select new HardmodeChartRecord(row.ChartId, Enum.Parse<ChartType>(chart.Type), row.Level,
                    row.Points, row.Holders, row.FolderSize, row.FolderCut))
            .ToArrayAsync(cancellationToken);
    }
}
