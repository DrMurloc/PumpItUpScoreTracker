using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.EventCompetition.Infrastructure.Entities;

namespace ScoreTracker.EventCompetition.Infrastructure;

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
        await database.Set<UserTournamentSessionEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<UserTournamentRegistrationEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<TournamentRoleEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<PhotoVerificationEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
