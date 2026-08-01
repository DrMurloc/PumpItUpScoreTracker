using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Identity.Infrastructure.Entities;

namespace ScoreTracker.Identity.Infrastructure;

internal sealed class EFAccountDeletionRepository : IAccountDeletionRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountDeletionRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Save(AccountDeletionRequest request, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<AccountDeletionRequestEntity>()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entity == null)
        {
            entity = new AccountDeletionRequestEntity { Id = request.Id };
            database.Set<AccountDeletionRequestEntity>().Add(entity);
        }

        entity.UserId = request.UserId;
        entity.RequestedAt = request.RequestedAt;
        entity.PurgeAfter = request.PurgeAfter;
        entity.CancelledAt = request.CancelledAt;
        entity.PurgedAt = request.PurgedAt;
        entity.WasPublic = request.WasPublic;
        entity.GameTag = request.GameTag;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<AccountDeletionRequest?> GetPending(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<AccountDeletionRequestEntity>()
            .Where(e => e.UserId == userId && e.CancelledAt == null && e.PurgedAt == null)
            .OrderByDescending(e => e.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity == null ? null : Map(entity);
    }

    public async Task<IEnumerable<AccountDeletionRequest>> GetPurgeable(DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<AccountDeletionRequestEntity>()
                .Where(e => e.CancelledAt == null && e.PurgedAt == null && e.PurgeAfter <= asOf)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    private static AccountDeletionRequest Map(AccountDeletionRequestEntity e)
    {
        return new AccountDeletionRequest(e.Id, e.UserId, e.RequestedAt, e.PurgeAfter, e.CancelledAt,
            e.PurgedAt, e.WasPublic, e.GameTag);
    }
}
