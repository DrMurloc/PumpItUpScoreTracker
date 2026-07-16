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

    public async Task LinkPlayer(MixEnum mix, string username, Guid userId, DateTimeOffset seenAt,
        CancellationToken ct)
    {
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
                p.AvatarMatched, p.Top50Overlap, p.Status, p.CreatedSnapshotId))
            .ToArrayAsync(ct);
    }

    public async Task<RenameProposal?> GetProposal(int id, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        return await database.Set<OfficialPlayerRenameProposalEntity>()
            .Where(p => p.Id == id)
            .Select(p => new RenameProposal(p.Id, p.OldPlayerId, p.NewPlayerId, p.OldUsername, p.NewUsername,
                p.AvatarMatched, p.Top50Overlap, p.Status, p.CreatedSnapshotId))
            .FirstOrDefaultAsync(ct);
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
