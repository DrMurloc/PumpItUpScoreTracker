using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFLimboChartRepository : ILimboChartRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFLimboChartRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlySet<Guid>> GetLimboCharts(MixEnum mix, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<LimboChartEntity>()
                .Where(e => e.MixId == mixId)
                .Select(e => e.ChartId)
                .ToArrayAsync(cancellationToken))
            .ToHashSet();
    }
}
