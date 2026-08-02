using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.Rivals.Infrastructure.Entities;

namespace ScoreTracker.Rivals.Infrastructure;

internal sealed class EFRivalRepository : IRivalRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFRivalRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<RivalEdge>> GetRivalsOwnedBy(Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<RivalEntity>()
            .AsNoTracking()
            .Where(r => r.OwnerUserId == ownerUserId)
            .OrderByDescending(r => r.AddedAt)
            .Select(r => new RivalEdge(r.Id, r.OwnerUserId, r.TargetUserId, r.TargetTag, r.AddedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RivalEdge>> GetRivalsTargeting(Guid targetUserId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<RivalEntity>()
            .AsNoTracking()
            .Where(r => r.TargetUserId == targetUserId)
            .OrderByDescending(r => r.AddedAt)
            .Select(r => new RivalEdge(r.Id, r.OwnerUserId, r.TargetUserId, r.TargetTag, r.AddedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<RivalEdge?> GetEdge(Guid edgeId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<RivalEntity>()
            .AsNoTracking()
            .Where(r => r.Id == edgeId)
            .Select(r => new RivalEdge(r.Id, r.OwnerUserId, r.TargetUserId, r.TargetTag, r.AddedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> EdgeExists(Guid ownerUserId, Guid? targetUserId, string? targetTag,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var edges = database.Set<RivalEntity>().Where(r => r.OwnerUserId == ownerUserId);
        return targetUserId == null
            ? await edges.AnyAsync(r => r.TargetTag == targetTag, cancellationToken)
            : await edges.AnyAsync(r => r.TargetUserId == targetUserId, cancellationToken);
    }

    public async Task Add(RivalEdge edge, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<RivalEntity>().AddAsync(new RivalEntity
        {
            Id = edge.Id,
            OwnerUserId = edge.OwnerUserId,
            TargetUserId = edge.TargetUserId,
            TargetTag = edge.TargetTag,
            AddedAt = edge.AddedAt
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> Remove(Guid edgeId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var removed = await database.Set<RivalEntity>()
            .Where(r => r.Id == edgeId)
            .ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }

    public async Task<bool> IsBlockedEitherWay(Guid userId, Guid otherUserId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<RivalBlockEntity>()
            .AnyAsync(b => (b.UserId == userId && b.BlockedUserId == otherUserId)
                           || (b.UserId == otherUserId && b.BlockedUserId == userId), cancellationToken);
    }

    public async Task Block(Guid userId, Guid blockedUserId, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // A block that left the arrows standing would be a setting, not a block — so both
        // directions go with it, and the row and the deletes commit together.
        await database.Set<RivalEntity>()
            .Where(r => (r.OwnerUserId == userId && r.TargetUserId == blockedUserId)
                        || (r.OwnerUserId == blockedUserId && r.TargetUserId == userId))
            .ExecuteDeleteAsync(cancellationToken);

        var alreadyBlocked = await database.Set<RivalBlockEntity>()
            .AnyAsync(b => b.UserId == userId && b.BlockedUserId == blockedUserId, cancellationToken);
        if (!alreadyBlocked)
        {
            await database.Set<RivalBlockEntity>().AddAsync(new RivalBlockEntity
            {
                UserId = userId,
                BlockedUserId = blockedUserId,
                CreatedAt = at
            }, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task Unblock(Guid userId, Guid blockedUserId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<RivalBlockEntity>()
            .Where(b => b.UserId == userId && b.BlockedUserId == blockedUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RivalBlockRecord>> GetBlockedBy(Guid userId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<RivalBlockEntity>()
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new RivalBlockRecord(b.BlockedUserId, b.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<int> PromoteTagToUser(string tag, Guid userId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // Somebody may already rival the account directly — they found them on the site before
        // the tag linked. Promoting the tag edge on top of that would trip the unique index, so
        // the redundant one is dropped instead. Same person either way.
        var owners = await database.Set<RivalEntity>()
            .Where(r => r.TargetTag == tag)
            .Select(r => r.OwnerUserId)
            .ToArrayAsync(cancellationToken);
        var alreadyDirect = await database.Set<RivalEntity>()
            .Where(r => r.TargetUserId == userId && owners.Contains(r.OwnerUserId))
            .Select(r => r.OwnerUserId)
            .ToArrayAsync(cancellationToken);

        await database.Set<RivalEntity>()
            .Where(r => r.TargetTag == tag && alreadyDirect.Contains(r.OwnerUserId))
            .ExecuteDeleteAsync(cancellationToken);

        // Nobody rivals themselves: an account whose own tag somebody stored is fine, but the
        // owner's own row would become a self-edge.
        await database.Set<RivalEntity>()
            .Where(r => r.TargetTag == tag && r.OwnerUserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        var promoted = await database.Set<RivalEntity>()
            .Where(r => r.TargetTag == tag)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.TargetUserId, userId)
                .SetProperty(r => r.TargetTag, (string?)null), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return promoted;
    }

    public async Task<int> RenameTag(string oldTag, string newTag, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var owners = await database.Set<RivalEntity>()
            .Where(r => r.TargetTag == oldTag)
            .Select(r => r.OwnerUserId)
            .ToArrayAsync(cancellationToken);
        var alreadyOnNewTag = await database.Set<RivalEntity>()
            .Where(r => r.TargetTag == newTag && owners.Contains(r.OwnerUserId))
            .Select(r => r.OwnerUserId)
            .ToArrayAsync(cancellationToken);

        await database.Set<RivalEntity>()
            .Where(r => r.TargetTag == oldTag && alreadyOnNewTag.Contains(r.OwnerUserId))
            .ExecuteDeleteAsync(cancellationToken);

        var renamed = await database.Set<RivalEntity>()
            .Where(r => r.TargetTag == oldTag)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.TargetTag, newTag), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return renamed;
    }
}
