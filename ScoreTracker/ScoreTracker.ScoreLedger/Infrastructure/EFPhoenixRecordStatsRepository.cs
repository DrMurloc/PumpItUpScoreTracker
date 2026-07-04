using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFPhoenixRecordStatsRepository : IPhoenixRecordStatsRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFPhoenixRecordStatsRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task UpdateScoreStats(Guid userId, IEnumerable<PhoenixRecordStats> stats,
        CancellationToken cancellationToken = default)
    {
        var statArray = stats.ToArray();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var chartIds = statArray.Select(s => s.ChartId).ToArray();
        var entities = await database.Set<PhoenixRecordStatsEntity>().Where(s => s.UserId == userId && chartIds.Contains(s.ChartId))
            .ToDictionaryAsync(e => e.ChartId, cancellationToken);
        var toCreate = new List<PhoenixRecordStatsEntity>();
        foreach (var stat in statArray)
            if (entities.ContainsKey(stat.ChartId))
            {
                entities[stat.ChartId].Pumbility = stat.Pumbility;
                entities[stat.ChartId].PumbilityPlus = stat.PumbilityPlus;
            }
            else
            {
                toCreate.Add(new PhoenixRecordStatsEntity
                {
                    ChartId = stat.ChartId,
                    UserId = userId,
                    // Phoenix until the port takes a mix (plan doc, port-threading commit).
                    MixId = MixIds.Phoenix,
                    PumbilityPlus = stat.PumbilityPlus,
                    Pumbility = stat.Pumbility
                });
            }

        await database.Set<PhoenixRecordStatsEntity>().AddRangeAsync(toCreate, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }
}
