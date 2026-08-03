using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.CommunityTools.Infrastructure;

internal sealed class EFToolKeyRepository : IToolKeyRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFToolKeyRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<ToolApiKeyRecord>> GetKeys(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ToolApiKeyEntity>()
                .Where(k => k.ToolId == toolId)
                .OrderByDescending(k => k.CreatedAt)
                .ToArrayAsync(cancellationToken))
            .Select(k => new ToolApiKeyRecord(k.Id, k.Name, k.Last4, k.CreatedAt, k.ExpiresAt,
                k.LastUsedAt, k.RevokedAt))
            .ToArray();
    }

    public async Task AddKey(Guid toolId, Guid keyId, string name, string hash, string last4,
        DateTimeOffset createdAt, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolApiKeyEntity>().AddAsync(new ToolApiKeyEntity
        {
            Id = keyId, ToolId = toolId, Name = name, KeyHash = hash, Last4 = last4,
            CreatedAt = createdAt, ExpiresAt = expiresAt
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeKey(Guid toolId, Guid keyId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolApiKeyEntity>()
            .Where(k => k.ToolId == toolId && k.Id == keyId && k.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.RevokedAt, at), cancellationToken);
    }

    public async Task<Guid?> ResolveToolByKeyHash(string hash, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var key = await database.Set<ToolApiKeyEntity>()
            .FirstOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);

        if (key is null || key.RevokedAt is not null) return null;
        if (key.ExpiresAt is not null && key.ExpiresAt <= now) return null;

        // Last-used is the only signal a maker has that a key is still carrying traffic, so it is
        // written on every call rather than sampled.
        key.LastUsedAt = now;
        await database.SaveChangesAsync(cancellationToken);
        return key.ToolId;
    }

    public async Task<IReadOnlyList<ToolInviteCodeRecord>> GetInviteCodes(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ToolInviteCodeEntity>()
            .Where(i => i.ToolId == toolId && i.RevokedAt == null)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new ToolInviteCodeRecord(i.InviteCode, i.Note, i.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task SetInviteCodeNote(Guid toolId, Guid code, string? note,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolInviteCodeEntity>()
            .Where(i => i.ToolId == toolId && i.InviteCode == code)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Note, note), cancellationToken);
    }

    public async Task AddInviteCode(Guid toolId, Guid code, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolInviteCodeEntity>().AddAsync(new ToolInviteCodeEntity
        {
            InviteCode = code, ToolId = toolId, CreatedAt = at
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeInviteCode(Guid toolId, Guid code, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolInviteCodeEntity>()
            .Where(i => i.ToolId == toolId && i.InviteCode == code && i.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.RevokedAt, at), cancellationToken);
    }

    public async Task<Guid?> ResolveToolByInviteCode(Guid code, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ToolInviteCodeEntity>()
            .Where(i => i.InviteCode == code && i.RevokedAt == null)
            .Select(i => (Guid?)i.ToolId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
