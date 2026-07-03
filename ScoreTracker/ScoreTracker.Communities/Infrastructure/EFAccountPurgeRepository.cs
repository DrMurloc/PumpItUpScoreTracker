using Microsoft.EntityFrameworkCore;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Communities.Infrastructure;

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
        await database.Set<CommunityMembershipEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
