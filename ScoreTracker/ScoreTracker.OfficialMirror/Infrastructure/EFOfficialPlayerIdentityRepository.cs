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

    public async Task<IReadOnlyList<RenameProposal>> WriteFindings(MixEnum mix,
        IReadOnlyCollection<RenameProposal> findings, CancellationToken ct)
    {
        if (findings.Count == 0) return Array.Empty<RenameProposal>();
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        // One live row per (old, new) pair — a replayed sweep must not stack duplicates on
        // the desk. A vanished tag with no candidate dedupes on the old id alone, since
        // NewPlayerId is null for every one of them.
        var oldIds = findings.Select(p => p.OldPlayerId).ToArray();
        var existingPairs = (await database.Set<OfficialPlayerRenameProposalEntity>()
                .Where(p => p.MixId == mixId && oldIds.Contains(p.OldPlayerId))
                .Select(p => new { p.OldPlayerId, p.NewPlayerId })
                .ToArrayAsync(ct))
            .Select(p => (p.OldPlayerId, p.NewPlayerId))
            .ToHashSet();

        var written = new List<(OfficialPlayerRenameProposalEntity Entity, RenameProposal Finding)>();
        foreach (var finding in findings.Where(p =>
                     !existingPairs.Contains((p.OldPlayerId, p.NewPlayerId))))
        {
            var entity = new OfficialPlayerRenameProposalEntity
            {
                MixId = mixId,
                OldPlayerId = finding.OldPlayerId,
                NewPlayerId = finding.NewPlayerId,
                OldUsername = finding.OldUsername,
                NewUsername = finding.NewUsername,
                Verdict = finding.Verdict,
                OldPlacements = finding.Evidence.OldPlacements,
                BoardsPresent = finding.Evidence.BoardsPresent,
                ExactNonPgMatches = finding.Evidence.ExactNonPgMatches,
                ExactPerfectGames = finding.Evidence.ExactPerfectGames,
                RunnerUpExactMatches = finding.Evidence.RunnerUpExactMatches,
                SuspiciousAbsences = finding.Evidence.SuspiciousAbsences,
                AvatarMatched = finding.Evidence.AvatarMatched,
                Status = ProposalStatuses.Pending,
                CreatedSnapshotId = finding.CreatedSnapshotId
            };
            await database.Set<OfficialPlayerRenameProposalEntity>().AddAsync(entity, ct);
            written.Add((entity, finding));
        }

        await database.SaveChangesAsync(ct);
        // Ids come back because the caller merges the conclusive ones straight away, and it
        // does that through the same accept path an admin uses rather than a second copy of it.
        return written.Select(w => w.Finding with { Id = w.Entity.Id, Mix = mix }).ToArray();
    }

    public async Task<IReadOnlyList<RenameProposal>> GetFindings(MixEnum mix, bool unresolvedOnly,
        CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var query = database.Set<OfficialPlayerRenameProposalEntity>().Where(p => p.MixId == mixId);
        if (unresolvedOnly) query = query.Where(p => p.Status == ProposalStatuses.Pending);
        return await query
            .OrderByDescending(p => p.Id)
            .Select(p => Project(p, mix))
            .ToArrayAsync(ct);
    }

    public async Task<RenameProposal?> GetProposal(int id, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var row = await database.Set<OfficialPlayerRenameProposalEntity>()
            .Where(p => p.Id == id)
            .Select(p => new { Entity = p, p.MixId })
            .FirstOrDefaultAsync(ct);
        return row == null ? null : Project(row.Entity, MixIds.ToEnum(row.MixId));
    }

    private static RenameProposal Project(OfficialPlayerRenameProposalEntity p, MixEnum mix) =>
        new(p.Id, p.OldPlayerId, p.NewPlayerId, p.OldUsername, p.NewUsername, p.Verdict,
            new RenameEvidence(p.OldPlacements, p.BoardsPresent, p.ExactNonPgMatches, p.ExactPerfectGames,
                p.RunnerUpExactMatches, p.SuspiciousAbsences, p.AvatarMatched),
            p.Status, p.CreatedSnapshotId, mix);

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

        // Where both tags hold a row on the same board in the same snapshot, one of them has
        // to go: they are one player now, and a board lists a player once. The PUBLISHED row
        // always wins that contest. A supplemented row is a stand-in the account's own ledger
        // provided for a board it never placed on, so keeping it over a real placement would
        // erase history the crawl actually recorded — and erase it from the official reading,
        // where the player would then be missing from a board they genuinely charted on.
        await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.PlayerId == newPlayerId && p.IsSupplemented &&
                        database.Set<OfficialLeaderboardPlacementEntity>().Any(o =>
                            o.PlayerId == oldPlayerId && o.SnapshotId == p.SnapshotId &&
                            o.LeaderboardId == p.LeaderboardId && !o.IsSupplemented))
            .ExecuteDeleteAsync(ct);

        // Whatever collision survives that is the transition week — both tags published on
        // one board — and there the new tag's row is the truth.
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
