using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Infrastructure;

internal sealed class EFToolMakerBanRepository : IToolMakerBanRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFToolMakerBanRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<ToolMakerBan?> GetBan(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<ToolMakerBanEntity>()
            .FirstOrDefaultAsync(b => b.UserId == userId, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ToolMakerBan>> GetBans(CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ToolMakerBanEntity>()
                .OrderByDescending(b => b.BannedAt).ToArrayAsync(cancellationToken))
            .Select(Map).ToArray();
    }

    /// <summary>Idempotent: banning someone already banned keeps the original date and notes.</summary>
    public async Task Ban(ToolMakerBan ban, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        if (await database.Set<ToolMakerBanEntity>()
                .AnyAsync(b => b.UserId == ban.UserId, cancellationToken)) return;

        await database.Set<ToolMakerBanEntity>().AddAsync(new ToolMakerBanEntity
        {
            UserId = ban.UserId,
            BannedAt = ban.BannedAt,
            BannedByUserId = ban.BannedByUserId,
            Notes = ban.Notes
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task Lift(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolMakerBanEntity>().Where(b => b.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SetNotes(Guid userId, string? notes, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolMakerBanEntity>().Where(b => b.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Notes, notes), cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> BannedAmong(IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new HashSet<Guid>();

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ToolMakerBanEntity>()
            .Where(b => userIds.Contains(b.UserId))
            .Select(b => b.UserId)
            .ToArrayAsync(cancellationToken)).ToHashSet();
    }

    private static ToolMakerBan Map(ToolMakerBanEntity entity)
    {
        return new ToolMakerBan(entity.UserId, entity.BannedAt, entity.BannedByUserId, entity.Notes);
    }
}
