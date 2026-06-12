using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartScoringLevelRepository : IChartScoringLevelRepository
{
    private static readonly IDictionary<MixEnum, Guid> MixGuids = new Dictionary<MixEnum, Guid>
    {
        { MixEnum.XX, Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8") },
        { MixEnum.Phoenix, Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B") }
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartScoringLevelRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveScoringLevel(MixEnum mix, Guid chartId, double? scoringLevel,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixGuids[mix];
        var existing = await database.ChartScoringLevel
            .FirstOrDefaultAsync(e => e.MixId == mixId && e.ChartId == chartId, cancellationToken);
        if (scoringLevel == null)
        {
            if (existing != null) database.ChartScoringLevel.Remove(existing);
        }
        else if (existing == null)
        {
            await database.ChartScoringLevel.AddAsync(new ChartScoringLevelEntity
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
        var mixId = MixGuids[mix];
        return await database.ChartScoringLevel.Where(e => e.MixId == mixId)
            .ToDictionaryAsync(e => e.ChartId, e => e.ScoringLevel, cancellationToken);
    }
}
