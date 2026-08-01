using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task UnlinkUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<OfficialPlayerEntity>()
            .Where(p => p.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.UserId, (Guid?)null)
                .SetProperty(p => p.UserIdSource, "None"), cancellationToken);
    }
}
