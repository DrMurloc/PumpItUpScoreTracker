using Microsoft.EntityFrameworkCore;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.CommunityTools.Infrastructure;

internal sealed class EFToolRepository : IToolRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFToolRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<Tool?> GetTool(Guid toolId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<ToolEntity>().FirstOrDefaultAsync(t => t.Id == toolId, cancellationToken);
        if (entity is null) return null;

        var mixIds = await database.Set<ToolMixSubscriptionEntity>()
            .Where(m => m.ToolId == toolId).Select(m => m.MixId).ToArrayAsync(cancellationToken);
        return Map(entity, mixIds);
    }

    public async Task<IReadOnlyList<Tool>> GetToolsOwnedBy(Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await LoadTools(t => t.OwnerUserId == ownerUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<Tool>> GetAllTools(CancellationToken cancellationToken = default)
    {
        return await LoadTools(_ => true, cancellationToken);
    }

    public async Task<IReadOnlyList<Tool>> GetToolsByVisibility(ToolVisibility visibility,
        CancellationToken cancellationToken = default)
    {
        var name = visibility.ToString();
        return await LoadTools(t => t.Visibility == name, cancellationToken);
    }

    public async Task Save(Tool tool, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<ToolEntity>().FirstOrDefaultAsync(t => t.Id == tool.Id, cancellationToken);
        if (entity is null)
        {
            entity = new ToolEntity { Id = tool.Id, OwnerUserId = tool.OwnerUserId, CreatedAt = tool.CreatedAt };
            await database.Set<ToolEntity>().AddAsync(entity, cancellationToken);
        }

        entity.Name = tool.Name.ToString();
        entity.Description = tool.Description;
        entity.Url = tool.Url?.ToString();
        entity.Visibility = tool.Visibility.ToString();
        entity.Kind = tool.Kind.ToString();
        entity.AcceptsAllToolsShare = tool.AcceptsAllToolsShare;
        entity.WebhookMode = tool.WebhookMode.ToString();
        entity.WebhookUrl = tool.WebhookUrl?.ToString();
        entity.ApprovedAt = tool.ApprovedAt;
        entity.RejectionReason = tool.RejectionReason;
        entity.WebhookUrlVerifiedAt = tool.WebhookUrlVerifiedAt;
        entity.RepositoryUrl = tool.RepositoryUrl?.ToString();
        entity.RepositoryOwner = tool.RepositoryOwner;
        entity.RepositoryCheckedAt = tool.RepositoryCheckedAt;
        entity.DiscordHandle = tool.DiscordHandle;
        entity.AgreedToRulesAt = tool.AgreedToRulesAt;

        // Mix subscriptions are replaced wholesale: the set is tiny and a diff would be more code
        // than it saves.
        var wanted = tool.Mixes.Select(MixIds.For).ToHashSet();
        var existing = await database.Set<ToolMixSubscriptionEntity>()
            .Where(m => m.ToolId == tool.Id).ToArrayAsync(cancellationToken);
        database.Set<ToolMixSubscriptionEntity>().RemoveRange(existing.Where(e => !wanted.Contains(e.MixId)));
        var have = existing.Select(e => e.MixId).ToHashSet();
        await database.Set<ToolMixSubscriptionEntity>().AddRangeAsync(
            wanted.Where(m => !have.Contains(m))
                .Select(m => new ToolMixSubscriptionEntity { Id = Guid.NewGuid(), ToolId = tool.Id, MixId = m }),
            cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTool(Guid toolId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolMixSubscriptionEntity>().Where(m => m.ToolId == toolId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<ToolInviteCodeEntity>().Where(i => i.ToolId == toolId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<ToolShareEntity>().Where(s => s.ToolId == toolId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<ToolBlockEntity>().Where(b => b.ToolId == toolId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<ToolApiKeyEntity>().Where(k => k.ToolId == toolId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<WebhookDeliveryEntity>().Where(d => d.ToolId == toolId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<ToolActivityEntity>().Where(a => a.ToolId == toolId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<ToolEntity>().Where(t => t.Id == toolId).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    ///     How many <b>other</b> people this tool can read. The maker is auto-granted a share when
    ///     they create it, so counting themselves would mean a brand-new tool reports one connected
    ///     player and its own maker is blocked from entering session mode by their own consent.
    /// </summary>
    public async Task<int> CountConnectedPlayers(Guid toolId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var ownerId = await database.Set<ToolEntity>().Where(t => t.Id == toolId)
            .Select(t => t.OwnerUserId).FirstOrDefaultAsync(cancellationToken);

        return (await GetReadablePlayerIds(toolId, cancellationToken)).Count(id => id != ownerId);
    }

    public async Task<IReadOnlyList<ToolShareRecord>> GetSharesForTool(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ToolShareEntity>()
            .Where(s => s.ToolId == toolId && s.RevokedAt == null)
            .ToArrayAsync(cancellationToken)).Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<ToolShareRecord>> GetSharesForUser(Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ToolShareEntity>()
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToArrayAsync(cancellationToken)).Select(Map).ToArray();
    }

    public async Task GrantShare(Guid toolId, Guid userId, ShareSource source, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var existing = await database.Set<ToolShareEntity>()
            .FirstOrDefaultAsync(s => s.ToolId == toolId && s.UserId == userId, cancellationToken);

        if (existing is null)
            await database.Set<ToolShareEntity>().AddAsync(new ToolShareEntity
            {
                Id = Guid.NewGuid(), ToolId = toolId, UserId = userId,
                Source = source.ToString(), GrantedAt = at
            }, cancellationToken);
        else
        {
            existing.RevokedAt = null;
            existing.Source = source.ToString();
            existing.GrantedAt = at;
        }

        // Connecting deliberately answers an earlier "not this one".
        await database.Set<ToolBlockEntity>().Where(b => b.ToolId == toolId && b.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeShare(Guid toolId, Guid userId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolShareEntity>()
            .Where(s => s.ToolId == toolId && s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RevokedAt, at), cancellationToken);
    }

    public async Task BlockTool(Guid toolId, Guid userId, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        if (!await database.Set<ToolBlockEntity>()
                .AnyAsync(b => b.ToolId == toolId && b.UserId == userId, cancellationToken))
            await database.Set<ToolBlockEntity>().AddAsync(new ToolBlockEntity
            {
                Id = Guid.NewGuid(), ToolId = toolId, UserId = userId, BlockedAt = at
            }, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UnblockTool(Guid toolId, Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ToolBlockEntity>().Where(b => b.ToolId == toolId && b.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<bool> GetShareWithAllTools(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var preference = await database.Set<ToolSharePreferenceEntity>()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return preference?.ShareWithAllTools ?? false;
    }

    public async Task SetShareWithAllTools(Guid userId, bool share, DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var preference = await database.Set<ToolSharePreferenceEntity>()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (preference is null)
            await database.Set<ToolSharePreferenceEntity>().AddAsync(new ToolSharePreferenceEntity
            {
                UserId = userId, ShareWithAllTools = share, SetAt = at
            }, cancellationToken);
        else
        {
            preference.ShareWithAllTools = share;
            preference.SetAt = at;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetToolIdsReading(Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var direct = await database.Set<ToolShareEntity>()
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .Select(s => s.ToolId).ToArrayAsync(cancellationToken);

        if (!await ShareWithAllTools(database, userId, cancellationToken)) return direct;

        var blocked = await database.Set<ToolBlockEntity>()
            .Where(b => b.UserId == userId).Select(b => b.ToolId).ToArrayAsync(cancellationToken);

        // The source gate and the maker ban are resolved in memory against the same predicates the
        // per-tool query uses. The candidate set is every pooled tool — tens of rows — and one rule
        // expressed twice is one rule that can drift into disagreeing with itself about who may read
        // a player's scores.
        var candidates = await database.Set<ToolEntity>()
            .Where(t => t.AcceptsAllToolsShare)
            // Session mode never arrives by blanket consent — it needs a moment the player saw.
            .Where(t => t.WebhookMode != nameof(WebhookMode.PiuGameSession))
            .Select(t => new
            {
                t.Id, t.OwnerUserId, t.RepositoryUrl, t.RepositoryCheckedAt, t.DiscordHandle
            })
            .ToArrayAsync(cancellationToken);

        var banned = await BannedOwners(database, candidates.Select(t => t.OwnerUserId), cancellationToken);
        var pool = candidates
            .Where(t => !banned.Contains(t.OwnerUserId))
            .Where(t => Tool.Shareable(t.Id, t.RepositoryUrl, t.RepositoryCheckedAt, t.DiscordHandle))
            .Select(t => t.Id)
            .ToArray();

        return direct.Union(pool.Except(blocked)).Distinct().ToArray();
    }

    public async Task<IReadOnlyList<Guid>> GetReadablePlayerIds(Guid toolId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var tool = await database.Set<ToolEntity>().FirstOrDefaultAsync(t => t.Id == toolId, cancellationToken);
        if (tool is null) return Array.Empty<Guid>();

        // A banned maker's tools read nobody at all, not even their own maker. Computed from the ban
        // row rather than written into the shares, so lifting the ban restores a working tool
        // instead of an empty shell — and the activity log a dispute is argued over survives.
        if ((await BannedOwners(database, new[] { tool.OwnerUserId }, cancellationToken)).Count > 0)
            return Array.Empty<Guid>();

        var direct = await database.Set<ToolShareEntity>()
            .Where(s => s.ToolId == toolId && s.RevokedAt == null)
            .Select(s => s.UserId).ToArrayAsync(cancellationToken);

        // A tool without a readable source and a reachable maker is excluded from the blanket pool,
        // exactly as a session-mode tool is. Deliberate grants already given survive it: cutting off
        // players who chose a tool because its maker mistyped a URL would punish the wrong people.
        //
        // Visibility is deliberately NOT part of this. Listing is a directory concern — what protects
        // a pooled player is the published source, the reachable maker, and the maker ban, all of
        // which a private tool is held to identically.
        if (!tool.AcceptsAllToolsShare
            || tool.WebhookMode == nameof(WebhookMode.PiuGameSession)
            || !Tool.Shareable(tool.Id, tool.RepositoryUrl, tool.RepositoryCheckedAt, tool.DiscordHandle))
            return direct.Distinct().ToArray();

        var blocked = await database.Set<ToolBlockEntity>()
            .Where(b => b.ToolId == toolId).Select(b => b.UserId).ToArrayAsync(cancellationToken);
        var pool = await database.Set<ToolSharePreferenceEntity>()
            .Where(p => p.ShareWithAllTools).Select(p => p.UserId).ToArrayAsync(cancellationToken);

        return direct.Union(pool.Except(blocked)).Distinct().ToArray();
    }

    public async Task<bool> CanRead(Guid toolId, Guid userId, CancellationToken cancellationToken = default)
    {
        return (await GetReadablePlayerIds(toolId, cancellationToken)).Contains(userId);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountKeysFor(IReadOnlyCollection<Guid> toolIds,
        DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        if (toolIds.Count == 0) return new Dictionary<Guid, int>();

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ToolApiKeyEntity>()
            .Where(k => toolIds.Contains(k.ToolId))
            // Live only. A revoked or expired key is not a reason to show a maker an API section.
            .Where(k => k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > asOf))
            .GroupBy(k => k.ToolId)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
    }

    private static async Task<IReadOnlySet<Guid>> BannedOwners(ChartAttemptDbContext database,
        IEnumerable<Guid> ownerUserIds, CancellationToken cancellationToken)
    {
        var ids = ownerUserIds.Distinct().ToArray();
        if (ids.Length == 0) return new HashSet<Guid>();

        return (await database.Set<ToolMakerBanEntity>()
            .Where(b => ids.Contains(b.UserId))
            .Select(b => b.UserId)
            .ToArrayAsync(cancellationToken)).ToHashSet();
    }

    private static async Task<bool> ShareWithAllTools(ChartAttemptDbContext database, Guid userId,
        CancellationToken cancellationToken)
    {
        return await database.Set<ToolSharePreferenceEntity>()
            .AnyAsync(p => p.UserId == userId && p.ShareWithAllTools, cancellationToken);
    }

    private async Task<IReadOnlyList<Tool>> LoadTools(
        System.Linq.Expressions.Expression<Func<ToolEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await database.Set<ToolEntity>().Where(predicate).ToArrayAsync(cancellationToken);
        var ids = entities.Select(e => e.Id).ToArray();
        var mixes = (await database.Set<ToolMixSubscriptionEntity>()
                .Where(m => ids.Contains(m.ToolId)).ToArrayAsync(cancellationToken))
            .GroupBy(m => m.ToolId)
            .ToDictionary(g => g.Key, g => g.Select(m => m.MixId).ToArray());

        return entities
            .Select(e => Map(e, mixes.TryGetValue(e.Id, out var ids2) ? ids2 : Array.Empty<Guid>()))
            .ToArray();
    }

    private static ToolShareRecord Map(ToolShareEntity entity)
    {
        return new ToolShareRecord(entity.ToolId, entity.UserId,
            Enum.Parse<ShareSource>(entity.Source), entity.GrantedAt);
    }

    private static Tool Map(ToolEntity entity, IReadOnlyCollection<Guid> mixIds)
    {
        return Tool.Rehydrate(entity.Id, entity.OwnerUserId, Name.From(entity.Name), entity.Description,
            entity.Url is null ? null : new Uri(entity.Url),
            Enum.Parse<ToolVisibility>(entity.Visibility),
            entity.AcceptsAllToolsShare,
            Enum.Parse<WebhookMode>(entity.WebhookMode),
            entity.WebhookUrl is null ? null : new Uri(entity.WebhookUrl),
            mixIds.Where(MixIds.IsKnown).Select(MixIds.ToEnum),
            entity.CreatedAt, entity.ApprovedAt, entity.RejectionReason, entity.WebhookUrlVerifiedAt,
            entity.RepositoryUrl is null ? null : new Uri(entity.RepositoryUrl),
            entity.RepositoryOwner, entity.RepositoryCheckedAt, entity.DiscordHandle,
            entity.AgreedToRulesAt,
            // Rows written before the column existed are all score-readers.
            string.IsNullOrEmpty(entity.Kind) ? ToolKind.Integrated : Enum.Parse<ToolKind>(entity.Kind));
    }
}
