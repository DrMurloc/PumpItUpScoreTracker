using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Ucs.Domain;
using ScoreTracker.Ucs.Infrastructure.Entities;

namespace ScoreTracker.Ucs.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<UcsChartLeaderboardEntryEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<UcsChartTagEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
