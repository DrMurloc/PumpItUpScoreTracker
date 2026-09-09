using Microsoft.EntityFrameworkCore;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.Communities.Infrastructure;

internal sealed class EFDiscordRoleRepository : IDiscordRoleRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFDiscordRoleRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<CommunityDiscordServerRecord?> GetServer(Guid communityId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<CommunityDiscordServerEntity>()
            .Where(s => s.CommunityId == communityId)
            .Select(s => new CommunityDiscordServerRecord(s.CommunityId, s.GuildId, s.GuildName, s.DesignatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityDiscordServerRecord>> GetAllServers(
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<CommunityDiscordServerEntity>()
            .Select(s => new CommunityDiscordServerRecord(s.CommunityId, s.GuildId, s.GuildName, s.DesignatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityDiscordServerRecord>> GetServersByGuild(ulong guildId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<CommunityDiscordServerEntity>()
            .Where(s => s.GuildId == guildId)
            .Select(s => new CommunityDiscordServerRecord(s.CommunityId, s.GuildId, s.GuildName, s.DesignatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task SaveServer(Guid communityId, ulong guildId, string guildName,
        DateTimeOffset designatedAt, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Set<CommunityDiscordServerEntity>()
            .FirstOrDefaultAsync(s => s.CommunityId == communityId, cancellationToken);
        if (existing != null)
        {
            existing.GuildId = guildId;
            existing.GuildName = guildName;
            existing.DesignatedAt = designatedAt;
        }
        else
        {
            await database.Set<CommunityDiscordServerEntity>().AddAsync(new CommunityDiscordServerEntity
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                GuildId = guildId,
                GuildName = guildName,
                DesignatedAt = designatedAt
            }, cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteServer(Guid communityId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<CommunityDiscordServerEntity>()
            .Where(s => s.CommunityId == communityId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityTitleRoleRecord>> GetMappings(Guid communityId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<CommunityTitleRoleEntity>()
            .Where(m => m.CommunityId == communityId)
            .OrderBy(m => m.TitleName)
            .Select(m => new CommunityTitleRoleRecord(m.TitleName, m.RoleId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task SaveMapping(Guid communityId, string titleName, ulong roleId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Set<CommunityTitleRoleEntity>()
            .FirstOrDefaultAsync(m => m.CommunityId == communityId && m.TitleName == titleName,
                cancellationToken);
        if (existing != null)
        {
            if (existing.RoleId == roleId) return;
            existing.RoleId = roleId;
        }
        else
        {
            await database.Set<CommunityTitleRoleEntity>().AddAsync(new CommunityTitleRoleEntity
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                TitleName = titleName,
                RoleId = roleId
            }, cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMapping(Guid communityId, string titleName, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<CommunityTitleRoleEntity>()
            .Where(m => m.CommunityId == communityId && m.TitleName == titleName)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityDiscordGrantRecord>> GetGrants(Guid communityId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<CommunityDiscordGrantEntity>()
            .Where(g => g.CommunityId == communityId)
            .Select(g => new CommunityDiscordGrantRecord(g.CommunityId, g.UserId, g.DiscordUserId,
                g.LastReconciledAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityDiscordGrantRecord>> GetGrantsForUser(Guid userId,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<CommunityDiscordGrantEntity>()
            .Where(g => g.UserId == userId)
            .Select(g => new CommunityDiscordGrantRecord(g.CommunityId, g.UserId, g.DiscordUserId,
                g.LastReconciledAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task SaveGrant(Guid communityId, Guid userId, ulong discordUserId,
        DateTimeOffset reconciledAt, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Set<CommunityDiscordGrantEntity>()
            .FirstOrDefaultAsync(g => g.CommunityId == communityId && g.UserId == userId, cancellationToken);
        if (existing != null)
        {
            existing.DiscordUserId = discordUserId;
            existing.LastReconciledAt = reconciledAt;
        }
        else
        {
            await database.Set<CommunityDiscordGrantEntity>().AddAsync(new CommunityDiscordGrantEntity
            {
                Id = Guid.NewGuid(),
                CommunityId = communityId,
                UserId = userId,
                DiscordUserId = discordUserId,
                LastReconciledAt = reconciledAt
            }, cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteGrant(Guid communityId, Guid userId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<CommunityDiscordGrantEntity>()
            .Where(g => g.CommunityId == communityId && g.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteGrantsForCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<CommunityDiscordGrantEntity>()
            .Where(g => g.CommunityId == communityId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteAllForCommunity(Guid communityId, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<CommunityDiscordGrantEntity>()
            .Where(g => g.CommunityId == communityId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<CommunityTitleRoleEntity>()
            .Where(m => m.CommunityId == communityId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<CommunityDiscordServerEntity>()
            .Where(s => s.CommunityId == communityId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
