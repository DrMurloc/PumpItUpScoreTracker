using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

internal sealed class EFChartScoringLevelRepository : IChartScoringLevelRepository
{

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartScoringLevelRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveScoringLevel(MixEnum mix, Guid chartId, double? scoringLevel,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var existing = await database.Set<ChartScoringLevelEntity>()
            .FirstOrDefaultAsync(e => e.MixId == mixId && e.ChartId == chartId, cancellationToken);
        if (scoringLevel == null)
        {
            if (existing != null) database.Set<ChartScoringLevelEntity>().Remove(existing);
        }
        else if (existing == null)
        {
            await database.Set<ChartScoringLevelEntity>().AddAsync(new ChartScoringLevelEntity
            {
                Id = Guid.NewGuid(),
                ChartId = chartId,
                MixId = mixId,
                ScoringLevel = scoringLevel.Value
            }, cancellationToken);
        }
        else
        {
            existing.ScoringLevel = scoringLevel.Value;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDictionary<Guid, double>> GetScoringLevels(MixEnum mix,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return await database.Set<ChartScoringLevelEntity>().Where(e => e.MixId == mixId)
            .ToDictionaryAsync(e => e.ChartId, e => e.ScoringLevel, cancellationToken);
    }
}
