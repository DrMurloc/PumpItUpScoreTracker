using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFOfficialPlayerIdentityRepository : IOfficialPlayerIdentityRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFOfficialPlayerIdentityRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<string> LinkPlayer(MixEnum mix, string username, Guid userId, DateTimeOffset seenAt,
        CancellationToken ct)
    {
        // The account page renders the tag with a space ("TAG #1234"); boards render it
        // without. Normalizing here keeps the import link on the same row the sweep writes.
        username = OfficialPlayerTag.Normalize(username);
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var entity = await database.Set<OfficialPlayerEntity>()
            .FirstOrDefaultAsync(p => p.MixId == mixId && p.Username == username, ct);
        if (entity == null)
        {
            entity = new OfficialPlayerEntity
            {
                MixId = mixId,
                Username = username,
                LastSeenAt = seenAt
            };
            await database.Set<OfficialPlayerEntity>().AddAsync(entity, ct);
        }

        entity.UserId = userId;
        entity.UserIdSource = "Import";
        await database.SaveChangesAsync(ct);
        return username;
    }

    public async Task<IReadOnlyList<PlayerDimension>> EnsureGameTagLinks(MixEnum mix,
        IReadOnlyCollection<(string Username, Guid UserId)> pairs, DateTimeOffset seenAt, CancellationToken ct)
    {
        if (pairs.Count == 0) return Array.Empty<PlayerDimension>();

        var normalized = pairs
            .Select(p => (Username: OfficialPlayerTag.Normalize(p.Username), p.UserId))
            .Where(p => p.Username.Length > 0)
            .ToArray();
        var tags = normalized.Select(p => p.Username).Distinct().ToArray();

        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var existing = await database.Set<OfficialPlayerEntity>()
            .Where(p => p.MixId == mixId && tags.Contains(p.Username))
            .ToDictionaryAsync(p => p.Username, ct);

        foreach (var (username, userId) in normalized)
        {
            if (!existing.TryGetValue(username, out var entity))
            {
                // A tag the crawl has never seen: this player is below every board's cut, which
                // is exactly who the supplemented reading exists to show. LastSeenAt is set once,
                // here, and the next sweep takes over if they ever place.
                entity = new OfficialPlayerEntity
                {
                    MixId = mixId, Username = username, LastSeenAt = seenAt,
                    UserId = userId, UserIdSource = "GameTag"
                };
                existing[username] = entity;
                await database.Set<OfficialPlayerEntity>().AddAsync(entity, ct);
                continue;
            }

            // An import-observed link was proved by logging into that account; this one is
            // inferred from the tag an import wrote onto the profile. Never downgrade.
            if (entity.UserId == null)
            {
                entity.UserId = userId;
                entity.UserIdSource = "GameTag";
            }
        }

        await database.SaveChangesAsync(ct);
        return normalized
            .Select(p => existing[p.Username])
            .DistinctBy(e => e.Id)
            .Select(e => new PlayerDimension(e.Id, e.Username,
                e.AvatarUrl == null ? null : new Uri(e.AvatarUrl, UriKind.Absolute), e.UserId, e.LastSeenAt))
            .ToArray();
    }

    public async Task RelinkUser(Guid fromUserId, Guid toUserId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialPlayerEntity>()
            .Where(p => p.UserId == fromUserId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.UserId, toUserId), ct);
    }

    public async Task WriteProposals(MixEnum mix, IReadOnlyCollection<RenameProposal> proposals,
        CancellationToken ct)
    {
        if (proposals.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        // One live proposal per (old, new) pair — a re-detected pair on a later sweep
        // must not stack duplicates in the admin queue.
        var oldIds = proposals.Select(p => p.OldPlayerId).ToArray();
        var existingPairs = (await database.Set<OfficialPlayerRenameProposalEntity>()
                .Where(p => p.MixId == mixId && oldIds.Contains(p.OldPlayerId))
                .Select(p => new { p.OldPlayerId, p.NewPlayerId })
                .ToArrayAsync(ct))
            .Select(p => (p.OldPlayerId, p.NewPlayerId))
            .ToHashSet();
        foreach (var proposal in proposals.Where(p =>
                     !existingPairs.Contains((p.OldPlayerId, p.NewPlayerId))))
            await database.Set<OfficialPlayerRenameProposalEntity>().AddAsync(
                new OfficialPlayerRenameProposalEntity
                {
                    MixId = mixId,
                    OldPlayerId = proposal.OldPlayerId,
                    NewPlayerId = proposal.NewPlayerId,
                    OldUsername = proposal.OldUsername,
                    NewUsername = proposal.NewUsername,
                    AvatarMatched = proposal.AvatarMatched,
                    Top50Overlap = proposal.Top50Overlap,
                    Status = ProposalStatuses.Pending,
                    CreatedSnapshotId = proposal.CreatedSnapshotId
                }, ct);
        await database.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RenameProposal>> GetProposals(MixEnum mix, string status, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await database.Set<OfficialPlayerRenameProposalEntity>()
            .Where(p => p.MixId == mixId && p.Status == status)
            .OrderByDescending(p => p.Id)
            .Select(p => new RenameProposal(p.Id, p.OldPlayerId, p.NewPlayerId, p.OldUsername, p.NewUsername,
                p.AvatarMatched, p.Top50Overlap, p.Status, p.CreatedSnapshotId, mix))
            .ToArrayAsync(ct);
    }

    public async Task<RenameProposal?> GetProposal(int id, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var row = await database.Set<OfficialPlayerRenameProposalEntity>()
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id, p.MixId, p.OldPlayerId, p.NewPlayerId, p.OldUsername, p.NewUsername, p.AvatarMatched,
                p.Top50Overlap, p.Status, p.CreatedSnapshotId
            })
            .FirstOrDefaultAsync(ct);
        return row == null
            ? null
            : new RenameProposal(row.Id, row.OldPlayerId, row.NewPlayerId, row.OldUsername, row.NewUsername,
                row.AvatarMatched, row.Top50Overlap, row.Status, row.CreatedSnapshotId,
                MixIds.ToEnum(row.MixId));
    }

    public async Task SetProposalStatus(int id, string status, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialPlayerRenameProposalEntity>()
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, status), ct);
    }

    public async Task MergePlayers(int oldPlayerId, int newPlayerId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await using var transaction = await database.Database.BeginTransactionAsync(ct);

        // Where both tags appear on the same board in the same snapshot (the transition
        // week), the new tag's row is the truth — drop the old row instead of colliding
        // with the placement key on re-point.
        await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.PlayerId == oldPlayerId &&
                        database.Set<OfficialLeaderboardPlacementEntity>().Any(n =>
                            n.PlayerId == newPlayerId && n.SnapshotId == p.SnapshotId &&
                            n.LeaderboardId == p.LeaderboardId))
            .ExecuteDeleteAsync(ct);
        await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.PlayerId == oldPlayerId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.PlayerId, newPlayerId), ct);

        await database.Set<OfficialWeeklyHighlightEntity>()
            .Where(h => h.PlayerId == oldPlayerId)
            .ExecuteUpdateAsync(u => u.SetProperty(h => h.PlayerId, newPlayerId), ct);
        await database.Set<OfficialWeeklyHighlightEntity>()
            .Where(h => h.DethronedPlayerId == oldPlayerId)
            .ExecuteUpdateAsync(u => u.SetProperty(h => h.DethronedPlayerId, newPlayerId), ct);

        // The merged player keeps any import-confirmed account link the old tag carried.
        var old = await database.Set<OfficialPlayerEntity>().FirstOrDefaultAsync(p => p.Id == oldPlayerId, ct);
        if (old != null)
        {
            if (old.UserId != null)
            {
                var target = await database.Set<OfficialPlayerEntity>()
                    .FirstAsync(p => p.Id == newPlayerId, ct);
                if (target.UserId == null)
                {
                    target.UserId = old.UserId;
                    target.UserIdSource = old.UserIdSource;
                }
            }

            database.Set<OfficialPlayerEntity>().Remove(old);
            await database.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
