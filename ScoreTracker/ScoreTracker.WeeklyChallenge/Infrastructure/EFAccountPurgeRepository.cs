using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.WeeklyChallenge.Domain;
using ScoreTracker.WeeklyChallenge.Infrastructure.Entities;

namespace ScoreTracker.WeeklyChallenge.Infrastructure;

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
        await database.Set<WeeklyUserEntry>().Where(e => e.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.Set<UserWeeklyPlacingEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
