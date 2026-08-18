using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.ChartComments.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Infrastructure;

internal sealed class EFCommentRestrictionRepository : ICommentRestrictionRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFCommentRestrictionRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Save(CommentRestriction restriction, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<CommentRestrictionEntity>()
            .FirstOrDefaultAsync(r => r.Id == restriction.Id, cancellationToken);

        if (entity == null)
        {
            entity = new CommentRestrictionEntity
            {
                Id = restriction.Id,
                UserId = restriction.UserId,
                CommunityId = restriction.CommunityId,
                RestrictedByUserId = restriction.RestrictedByUserId,
                Reason = restriction.Reason,
                CreatedAt = restriction.CreatedAt
            };
            await database.Set<CommentRestrictionEntity>().AddAsync(entity, cancellationToken);
        }

        // Only the lift mutates after the fact; who muted whom and why are history, not state.
        entity.LiftedAt = restriction.LiftedAt;

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<CommentRestriction?> GetActive(Guid userId, Guid communityId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<CommentRestrictionEntity>().AsNoTracking()
            .Where(r => r.UserId == userId && r.CommunityId == communityId && r.LiftedAt == null)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity == null ? null : Hydrate(entity);
    }

    public async Task<IReadOnlyList<CommentRestriction>> GetActiveForCommunity(Guid communityId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await database.Set<CommentRestrictionEntity>().AsNoTracking()
            .Where(r => r.CommunityId == communityId && r.LiftedAt == null)
            .OrderByDescending(r => r.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return entities.Select(Hydrate).ToArray();
    }

    public async Task<IReadOnlyList<CommentRestriction>> GetActiveForUser(Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await database.Set<CommentRestrictionEntity>().AsNoTracking()
            .Where(r => r.UserId == userId && r.LiftedAt == null)
            .ToArrayAsync(cancellationToken);

        return entities.Select(Hydrate).ToArray();
    }

    private static CommentRestriction Hydrate(CommentRestrictionEntity entity)
    {
        return CommentRestriction.FromStorage(new CommentRestrictionState(entity.Id, entity.UserId,
            entity.CommunityId, entity.RestrictedByUserId, entity.Reason, entity.CreatedAt,
            entity.LiftedAt));
    }
}
