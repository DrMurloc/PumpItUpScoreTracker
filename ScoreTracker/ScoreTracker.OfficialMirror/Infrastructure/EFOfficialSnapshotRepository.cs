using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFOfficialSnapshotRepository : IOfficialSnapshotRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFOfficialSnapshotRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<int> CreateRun(MixEnum mix, bool isBaseline, DateTimeOffset startedAt, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var entity = new OfficialLeaderboardSnapshotEntity
        {
            MixId = MixIds.For(mix),
            StartedAt = startedAt,
            LastProgressAt = startedAt,
            IsBaseline = isBaseline,
            Stage = "Created"
        };
        await database.Set<OfficialLeaderboardSnapshotEntity>().AddAsync(entity, ct);
        await database.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateProgress(int snapshotId, string stage, int boardsExpected, int boardsWritten,
        int boardsSkipped, DateTimeOffset at, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.Id == snapshotId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Stage, stage)
                .SetProperty(s => s.BoardsExpected, boardsExpected)
                .SetProperty(s => s.BoardsWritten, boardsWritten)
                .SetProperty(s => s.BoardsSkipped, boardsSkipped)
                .SetProperty(s => s.LastProgressAt, at), ct);
    }

    public async Task MarkFailed(int snapshotId, string error, CancellationToken ct)
    {
        // Truncated to the column so a novel-length stack trace can't fail the failure write.
        var stored = error.Length > 2000 ? error[..2000] : error;
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.Id == snapshotId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Error, stored), ct);
    }

    public async Task Seal(int snapshotId, DateTimeOffset completedAt, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.Id == snapshotId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.CompletedAt, completedAt)
                .SetProperty(s => s.Stage, "Sealed"), ct);
    }

    public async Task PurgeUnsealed(MixEnum mix, DateTimeOffset olderThan, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var staleIds = await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.MixId == mixId && s.CompletedAt == null && s.StartedAt < olderThan)
            .Select(s => s.Id)
            .ToArrayAsync(ct);
        if (staleIds.Length == 0) return;

        await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => staleIds.Contains(p.SnapshotId)).ExecuteDeleteAsync(ct);
        await database.Set<OfficialChartPopularityEntity>()
            .Where(p => staleIds.Contains(p.SnapshotId)).ExecuteDeleteAsync(ct);
        await database.Set<OfficialWeeklyHighlightEntity>()
            .Where(h => staleIds.Contains(h.SnapshotId)).ExecuteDeleteAsync(ct);
        await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => staleIds.Contains(s.Id)).ExecuteDeleteAsync(ct);
    }

    public async Task<bool> HasLiveRun(MixEnum mix, DateTimeOffset heartbeatCutoff, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await database.Set<OfficialLeaderboardSnapshotEntity>()
            .AnyAsync(s => s.MixId == mixId && s.CompletedAt == null && s.LastProgressAt >= heartbeatCutoff, ct);
    }

    public async Task<SnapshotRun?> GetLatestSealed(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var entity = await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.MixId == mixId && s.CompletedAt != null)
            .OrderByDescending(s => s.CompletedAt)
            .FirstOrDefaultAsync(ct);
        return entity == null ? null : ToRun(entity);
    }

    public async Task<SnapshotRun?> GetSealedBefore(MixEnum mix, int snapshotId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var completedAt = await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.Id == snapshotId)
            .Select(s => s.CompletedAt)
            .FirstOrDefaultAsync(ct);
        var entity = await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.MixId == mixId && s.CompletedAt != null && s.Id != snapshotId &&
                        (completedAt == null || s.CompletedAt < completedAt))
            .OrderByDescending(s => s.CompletedAt)
            .FirstOrDefaultAsync(ct);
        return entity == null ? null : ToRun(entity);
    }

    public async Task<IReadOnlyList<SnapshotRun>> GetSealedAscending(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return (await database.Set<OfficialLeaderboardSnapshotEntity>()
                .Where(s => s.MixId == mixId && s.CompletedAt != null)
                .OrderBy(s => s.CompletedAt)
                .ToArrayAsync(ct))
            .Select(ToRun).ToArray();
    }

    public async Task<IReadOnlyList<SnapshotRun>> GetRecentRuns(MixEnum mix, int count, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return (await database.Set<OfficialLeaderboardSnapshotEntity>()
                .Where(s => s.MixId == mixId)
                .OrderByDescending(s => s.StartedAt)
                .Take(count)
                .ToArrayAsync(ct))
            .Select(ToRun).ToArray();
    }

    public async Task<bool> AnySealed(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await database.Set<OfficialLeaderboardSnapshotEntity>()
            .AnyAsync(s => s.MixId == mixId && s.CompletedAt != null, ct);
    }

    public async Task<IReadOnlyList<BoardDimension>> GetBoards(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await database.Set<OfficialLeaderboardEntity>()
            .Where(b => b.MixId == mixId)
            .Select(b => new BoardDimension(b.Id, b.LeaderboardType, b.Name, b.ChartId, b.ChartType, b.Level))
            .ToArrayAsync(ct);
    }

    public async Task<BoardDimension> EnsureBoard(MixEnum mix, string leaderboardType, string name, Guid? chartId,
        string? chartType, int? level, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var entity = await database.Set<OfficialLeaderboardEntity>()
            .FirstOrDefaultAsync(b => b.MixId == mixId && b.LeaderboardType == leaderboardType && b.Name == name,
                ct);
        if (entity == null)
        {
            entity = new OfficialLeaderboardEntity
            {
                MixId = mixId,
                LeaderboardType = leaderboardType,
                Name = name,
                ChartId = chartId,
                ChartType = chartType,
                Level = level
            };
            await database.Set<OfficialLeaderboardEntity>().AddAsync(entity, ct);
            await database.SaveChangesAsync(ct);
        }
        else if (entity.ChartId != chartId || entity.ChartType != chartType || entity.Level != level)
        {
            entity.ChartId = chartId;
            entity.ChartType = chartType;
            entity.Level = level;
            await database.SaveChangesAsync(ct);
        }

        return new BoardDimension(entity.Id, entity.LeaderboardType, entity.Name, entity.ChartId, entity.ChartType,
            entity.Level);
    }

    public async Task<IReadOnlyList<PlayerDimension>> EnsurePlayers(MixEnum mix,
        IReadOnlyCollection<(string Username, Uri? Avatar)> players, DateTimeOffset seenAt, CancellationToken ct)
    {
        if (players.Count == 0) return Array.Empty<PlayerDimension>();

        // Tags normalize at the storage seam so both scrape shapes ("TAG#1" and "TAG #1")
        // land on one player row; duplicates collapse to the entry that carries an avatar.
        var normalized = players
            .Select(p => (Username: OfficialPlayerTag.Normalize(p.Username), p.Avatar))
            .GroupBy(p => p.Username, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(p => p.Avatar != null).First())
            .ToArray();

        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var result = new List<PlayerDimension>(normalized.Length);

        foreach (var chunk in normalized.Chunk(500))
        {
            var names = chunk.Select(p => p.Username).ToArray();
            var existing = await database.Set<OfficialPlayerEntity>()
                .Where(p => p.MixId == mixId && names.Contains(p.Username))
                .ToDictionaryAsync(p => p.Username, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var (username, avatar) in chunk)
            {
                if (existing.TryGetValue(username, out var entity))
                {
                    entity.LastSeenAt = seenAt;
                    var incoming = avatar?.ToString();
                    if (incoming != null && entity.AvatarUrl != incoming) entity.AvatarUrl = incoming;
                }
                else
                {
                    entity = new OfficialPlayerEntity
                    {
                        MixId = mixId,
                        Username = username,
                        AvatarUrl = avatar?.ToString(),
                        LastSeenAt = seenAt
                    };
                    await database.Set<OfficialPlayerEntity>().AddAsync(entity, ct);
                    existing[username] = entity;
                }
            }

            await database.SaveChangesAsync(ct);
            result.AddRange(chunk.Select(p => ToPlayer(existing[p.Username])));
        }

        return result;
    }

    public async Task<IReadOnlySet<int>> GetSeenPlayerIds(MixEnum mix, int beforeSnapshotId, PlacementScope scope,
        CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        // Unsealed leftovers count as "seen" — the conservative direction for a debut.
        return (await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
                .Where(p => p.SnapshotId < beforeSnapshotId &&
                            database.Set<OfficialLeaderboardSnapshotEntity>()
                                .Any(s => s.Id == p.SnapshotId && s.MixId == mixId))
                .Select(p => p.PlayerId)
                .Distinct()
                .ToArrayAsync(ct))
            .ToHashSet();
    }

    public async Task<IReadOnlyList<PlayerDimension>> GetPlayers(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return (await database.Set<OfficialPlayerEntity>()
                .Where(p => p.MixId == mixId)
                .ToArrayAsync(ct))
            .Select(ToPlayer).ToArray();
    }

    /// <summary>
    ///     Rows per SaveChanges. The sweep calls this once per board and never notices, but the
    ///     supplemented roll-up hands over a whole mix at once — nearly 200,000 rows on Phoenix
    ///     — and one tracked transaction that size does not complete. A fresh context per batch
    ///     keeps change tracking linear rather than quadratic.
    /// </summary>
    private const int WriteBatch = 2000;

    public async Task WritePlacements(int snapshotId, IReadOnlyCollection<PlacementRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;

        // Batching gives up atomicity across the whole write, which is safe here: an unsealed
        // snapshot is invisible until the sweep seals it, and a roll-up clears its own
        // supplemented rows before writing, so a half-finished run is replaced rather than
        // merged into.
        foreach (var batch in rows.Chunk(WriteBatch))
        {
            await using var database = await _factory.CreateDbContextAsync(ct);
            await database.Set<OfficialLeaderboardPlacementEntity>().AddRangeAsync(batch.Select(r =>
                new OfficialLeaderboardPlacementEntity
                {
                    SnapshotId = snapshotId,
                    LeaderboardId = r.LeaderboardId,
                    PlayerId = r.PlayerId,
                    Place = r.Place,
                    Score = r.Score,
                    IsSupplemented = r.IsSupplemented
                }), ct);
            await database.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    ///     The one place the supplemented filter is written. Every placement read in this
    ///     class routes through it, so a new read cannot forget the predicate — it has to
    ///     take a <see cref="PlacementScope" /> to compile, and the scope has no default.
    /// </summary>
    private static IQueryable<OfficialLeaderboardPlacementEntity> Scoped(
        IQueryable<OfficialLeaderboardPlacementEntity> placements, PlacementScope scope) =>
        scope == PlacementScope.OfficialOnly ? placements.Where(p => !p.IsSupplemented) : placements;

    public async Task<IReadOnlyList<PlacementRow>> GetPlacements(int snapshotId, PlacementScope scope,
        CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        return await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
            .Where(p => p.SnapshotId == snapshotId)
            .Select(p => new PlacementRow(p.LeaderboardId, p.PlayerId, p.Place, p.Score, p.IsSupplemented))
            .ToArrayAsync(ct);
    }

    public async Task DeleteSupplementedPlacements(int snapshotId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.SnapshotId == snapshotId && p.IsSupplemented)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<(int Players, int Rows)> CountSupplemented(int snapshotId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var rows = database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.SnapshotId == snapshotId && p.IsSupplemented);
        return (await rows.Select(p => p.PlayerId).Distinct().CountAsync(ct), await rows.CountAsync(ct));
    }

    public async Task<bool> AnySupplemented(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.IsSupplemented)
            .Join(database.Set<OfficialLeaderboardSnapshotEntity>()
                    .Where(s => s.MixId == mixId && s.CompletedAt != null),
                p => p.SnapshotId, s => s.Id, (p, _) => p)
            .AnyAsync(ct);
    }

    public async Task WritePopularity(int snapshotId, IReadOnlyCollection<(Guid ChartId, int Place)> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialChartPopularityEntity>().AddRangeAsync(rows.Select(r =>
            new OfficialChartPopularityEntity
            {
                SnapshotId = snapshotId,
                ChartId = r.ChartId,
                Place = r.Place
            }), ct);
        await database.SaveChangesAsync(ct);
    }

    public async Task DeletePopularity(int snapshotId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialChartPopularityEntity>()
            .Where(p => p.SnapshotId == snapshotId)
            .ExecuteDeleteAsync(ct);
    }


    public async Task<IReadOnlyList<(Guid ChartId, int Place)>> GetPopularity(int snapshotId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        return (await database.Set<OfficialChartPopularityEntity>()
                .Where(p => p.SnapshotId == snapshotId)
                .Select(p => new { p.ChartId, p.Place })
                .ToArrayAsync(ct))
            .Select(p => (p.ChartId, p.Place)).ToArray();
    }

    public async Task<IReadOnlyList<PlayerDimension>> GetPlayersByIds(IReadOnlyCollection<int> playerIds,
        CancellationToken ct)
    {
        if (playerIds.Count == 0) return Array.Empty<PlayerDimension>();
        await using var database = await _factory.CreateDbContextAsync(ct);
        var results = new List<PlayerDimension>(playerIds.Count);
        foreach (var chunk in playerIds.Chunk(1000))
        {
            var ids = chunk;
            results.AddRange((await database.Set<OfficialPlayerEntity>()
                    .Where(p => ids.Contains(p.Id))
                    .ToArrayAsync(ct))
                .Select(ToPlayer));
        }

        return results;
    }

    public async Task<PlayerDimension?> GetPlayerByUsername(MixEnum mix, string username, CancellationToken ct)
    {
        // Lookups accept either scrape shape — storage holds the normalized tag.
        var tag = OfficialPlayerTag.Normalize(username);
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var entity = await database.Set<OfficialPlayerEntity>()
            .FirstOrDefaultAsync(p => p.MixId == mixId && p.Username == tag, ct);
        return entity == null ? null : ToPlayer(entity);
    }

    public async Task<PlayerDimension?> GetPlayerByUserId(MixEnum mix, Guid userId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        // The same rule as the bulk read: where an account still owns two rows in a mix, the one
        // the crawl saw most recently is the tag in use. Unordered, SQL Server hands back
        // whichever it likes — typically the older row — and the single and bulk reads disagreed
        // about one player's tag.
        var entity = await database.Set<OfficialPlayerEntity>()
            .Where(p => p.MixId == mixId && p.UserId == userId)
            .OrderByDescending(p => p.LastSeenAt)
            .FirstOrDefaultAsync(ct);
        return entity == null ? null : ToPlayer(entity);
    }

    public async Task<IReadOnlyList<PlayerDimension>> GetPlayersByUserIds(MixEnum mix,
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return Array.Empty<PlayerDimension>();
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var results = new List<PlayerDimension>(userIds.Count);
        foreach (var chunk in userIds.Chunk(1000))
        {
            var ids = chunk;
            results.AddRange((await database.Set<OfficialPlayerEntity>()
                    .Where(p => p.MixId == mixId && p.UserId != null && ids.Contains(p.UserId.Value))
                    .ToArrayAsync(ct))
                .Select(ToPlayer));
        }

        return results;
    }

    public async Task<IReadOnlyList<string>> GetPlayerNames(MixEnum mix, PlacementScope scope,
        CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var latest = await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.MixId == mixId && s.CompletedAt != null)
            .OrderByDescending(s => s.CompletedAt)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
        if (latest == null) return Array.Empty<string>();

        var placed = Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
            .Where(p => p.SnapshotId == latest.Value)
            .Select(p => p.PlayerId);

        return await database.Set<OfficialPlayerEntity>()
            .Where(p => p.MixId == mixId && placed.Contains(p.Id))
            .OrderBy(p => p.Username)
            .Select(p => p.Username)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<OfficialPlayerRecord>> SearchPlayersInSnapshot(int snapshotId, string term,
        int take, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(term) || take <= 0) return Array.Empty<OfficialPlayerRecord>();

        await using var database = await _factory.CreateDbContextAsync(ct);
        // Player-first with an EXISTS, not placements-first with a DISTINCT. The placement table
        // holds every board position in the snapshot and the player dimension holds one row per
        // tag, so filtering the dimension by name and then testing membership reads orders of
        // magnitude fewer rows than de-duplicating the join's output.
        // Match rank in the ORDER BY: an exact tag, then a prefix, then anything containing the
        // term — a one-letter tag has to come first for a one-letter term.
        var rows = await database.Set<OfficialPlayerEntity>()
            .Where(x => x.Username.Contains(term))
            .Where(x => database.Set<OfficialLeaderboardPlacementEntity>()
                .Any(p => p.SnapshotId == snapshotId && p.PlayerId == x.Id))
            .OrderBy(x => x.Username == term ? 0 : x.Username.StartsWith(term) ? 1 : 2)
            .ThenBy(x => x.Username)
            .Select(x => new { x.Id, x.Username, x.AvatarUrl, x.UserId })
            .Take(take)
            .ToArrayAsync(ct);
        return rows
            .Select(x => new OfficialPlayerRecord(x.Id, x.Username,
                x.AvatarUrl == null ? null : new Uri(x.AvatarUrl), x.UserId))
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> FilterNamesInSnapshot(int snapshotId,
        IReadOnlyCollection<string> names, CancellationToken ct)
    {
        if (names.Count == 0) return Array.Empty<string>();

        await using var database = await _factory.CreateDbContextAsync(ct);
        return await database.Set<OfficialPlayerEntity>()
            .Where(x => names.Contains(x.Username))
            .Where(x => database.Set<OfficialLeaderboardPlacementEntity>()
                .Any(p => p.SnapshotId == snapshotId && p.PlayerId == x.Id))
            .Select(x => x.Username)
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<PlayerDimension>> GetPlayersByUsernames(MixEnum mix,
        IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        if (usernames.Count == 0) return Array.Empty<PlayerDimension>();
        // Lookups accept either scrape shape — storage holds the normalized tag.
        var tags = usernames.Select(OfficialPlayerTag.Normalize).Distinct().ToArray();
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var entities = await database.Set<OfficialPlayerEntity>()
            .Where(p => p.MixId == mixId && tags.Contains(p.Username))
            .ToArrayAsync(ct);
        return entities.Select(ToPlayer).ToArray();
    }

    public async Task<IReadOnlyList<PlayerChartPlacement>> GetChartPlacementsFor(int snapshotId,
        IReadOnlyCollection<int> playerIds, IReadOnlyCollection<Guid> chartIds, CancellationToken ct)
    {
        if (playerIds.Count == 0 || chartIds.Count == 0) return Array.Empty<PlayerChartPlacement>();
        await using var database = await _factory.CreateDbContextAsync(ct);
        return await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.SnapshotId == snapshotId && playerIds.Contains(p.PlayerId))
            .Join(database.Set<OfficialLeaderboardEntity>()
                    .Where(b => b.LeaderboardType == LeaderboardTypes.Chart
                                && b.ChartId != null && chartIds.Contains(b.ChartId.Value)),
                p => p.LeaderboardId, b => b.Id,
                (p, b) => new PlayerChartPlacement(p.PlayerId, b.ChartId!.Value, p.Place, p.Score))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<PlayerChartHistoryRow>> GetChartHistoryFor(MixEnum mix,
        IReadOnlyCollection<int> playerIds, ChartType chartType, int minimumLevel, int maximumLevel,
        PlacementScope scope, CancellationToken ct)
    {
        if (playerIds.Count == 0) return Array.Empty<PlayerChartHistoryRow>();
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var typeName = chartType.ToString();
        // The board dimension carries the chart's type and level, so the catalog is never joined:
        // the mirror already wrote down what it swept. Grouped rather than ordered-and-first
        // because only the best matters and the group-by stays on the covering index.
        return await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
            .Where(p => playerIds.Contains(p.PlayerId))
            .Join(database.Set<OfficialLeaderboardEntity>()
                    .Where(b => b.MixId == mixId && b.LeaderboardType == LeaderboardTypes.Chart
                                                 && b.ChartId != null && b.ChartType == typeName
                                                 && b.Level != null && b.Level >= minimumLevel
                                                 && b.Level <= maximumLevel),
                p => p.LeaderboardId, b => b.Id,
                (p, b) => new { p.PlayerId, ChartId = b.ChartId!.Value, Level = b.Level!.Value, p.Score })
            .GroupBy(x => new { x.PlayerId, x.ChartId, x.Level })
            .Select(g => new PlayerChartHistoryRow(g.Key.PlayerId, g.Key.ChartId, g.Key.Level,
                (int)g.Max(x => x.Score)))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<PlayerChartHistoryRow>> GetChartHistoryOn(MixEnum mix,
        IReadOnlyCollection<int> playerIds, IReadOnlyCollection<Guid> chartIds, PlacementScope scope,
        CancellationToken ct)
    {
        if (playerIds.Count == 0 || chartIds.Count == 0) return Array.Empty<PlayerChartHistoryRow>();
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
            .Where(p => playerIds.Contains(p.PlayerId))
            .Join(database.Set<OfficialLeaderboardEntity>()
                    .Where(b => b.MixId == mixId && b.LeaderboardType == LeaderboardTypes.Chart
                                                 && b.ChartId != null && chartIds.Contains(b.ChartId.Value)),
                p => p.LeaderboardId, b => b.Id,
                (p, b) => new { p.PlayerId, ChartId = b.ChartId!.Value, Level = b.Level ?? 0, p.Score })
            .GroupBy(x => new { x.PlayerId, x.ChartId, x.Level })
            .Select(g => new PlayerChartHistoryRow(g.Key.PlayerId, g.Key.ChartId, g.Key.Level,
                (int)g.Max(x => x.Score)))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<BoardChartHistoryRow>> GetEveryChartHistory(MixEnum mix,
        PlacementScope scope, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        // The same shape as the two bounded reads above, with the bounds taken off and the type
        // carried out instead of filtered on. Every type and level the boards hold: the caller
        // answers a chart dialog as well as a pool, and a CO-OP chart has a board and no pool.
        var rows = await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
            .Join(database.Set<OfficialLeaderboardEntity>()
                    .Where(b => b.MixId == mixId && b.LeaderboardType == LeaderboardTypes.Chart
                                                 && b.ChartId != null && b.Level != null),
                p => p.LeaderboardId, b => b.Id,
                (p, b) => new
                {
                    p.PlayerId, ChartId = b.ChartId!.Value, Level = b.Level!.Value, b.ChartType, p.Score
                })
            .GroupBy(x => new { x.PlayerId, x.ChartId, x.Level, x.ChartType })
            .Select(g => new
            {
                g.Key.PlayerId, g.Key.ChartId, g.Key.Level, g.Key.ChartType, Score = (int)g.Max(x => x.Score)
            })
            .ToArrayAsync(ct);

        // A board whose type this build has never heard of is dropped rather than guessed at.
        return rows
            .Where(r => Enum.TryParse<ChartType>(r.ChartType, out _))
            .Select(r => new BoardChartHistoryRow(r.PlayerId, r.ChartId, r.Level, r.Score,
                Enum.Parse<ChartType>(r.ChartType!)))
            .ToArray();
    }

    public async Task<IReadOnlyList<PlacementRow>> GetBoardPlacements(int snapshotId, int leaderboardId,
        PlacementScope scope, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        return await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
            .Where(p => p.SnapshotId == snapshotId && p.LeaderboardId == leaderboardId)
            .OrderBy(p => p.Place)
            .Select(p => new PlacementRow(p.LeaderboardId, p.PlayerId, p.Place, p.Score, p.IsSupplemented))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<PlacementRow>> GetBoardPlacements(int snapshotId,
        IReadOnlyCollection<int> leaderboardIds, PlacementScope scope, CancellationToken ct)
    {
        if (leaderboardIds.Count == 0) return Array.Empty<PlacementRow>();
        await using var database = await _factory.CreateDbContextAsync(ct);
        return await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
            .Where(p => p.SnapshotId == snapshotId && leaderboardIds.Contains(p.LeaderboardId))
            .OrderBy(p => p.LeaderboardId).ThenBy(p => p.Place)
            .Select(p => new PlacementRow(p.LeaderboardId, p.PlayerId, p.Place, p.Score, p.IsSupplemented))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<PlacementDetail>> GetPlacementDetails(int snapshotId, PlacementScope scope,
        CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        return await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
            .Where(p => p.SnapshotId == snapshotId)
            .Join(database.Set<OfficialLeaderboardEntity>(), p => p.LeaderboardId, b => b.Id,
                (p, b) => new PlacementDetail(p.PlayerId, p.LeaderboardId, b.LeaderboardType, b.Name, b.ChartId,
                    b.ChartType, b.Level, p.Place, p.Score, p.IsSupplemented))
            .ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<PlayerTimelineRow>> GetPlayerTimeline(int playerId, PlacementScope scope,
        CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        // Ordering must happen over the anonymous projection — EF cannot translate an
        // OrderBy that reaches into a constructed record's member.
        return (await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
                .Where(p => p.PlayerId == playerId)
                .Join(database.Set<OfficialLeaderboardSnapshotEntity>().Where(s => s.CompletedAt != null),
                    p => p.SnapshotId, s => s.Id, (p, s) => new { p, s })
                .Join(database.Set<OfficialLeaderboardEntity>(), ps => ps.p.LeaderboardId, b => b.Id,
                    (ps, b) => new
                    {
                        ps.p.SnapshotId,
                        CompletedAt = ps.s.CompletedAt!.Value,
                        b.LeaderboardType,
                        b.Name,
                        b.ChartId,
                        ps.p.Place,
                        ps.p.Score,
                        ps.p.IsSupplemented
                    })
                .OrderBy(r => r.CompletedAt)
                .ToArrayAsync(ct))
            .Select(r => new PlayerTimelineRow(r.SnapshotId, r.CompletedAt, r.LeaderboardType, r.Name, r.ChartId,
                r.Place, r.Score, r.IsSupplemented))
            .ToArray();
    }

    public async Task<IReadOnlyList<(int SnapshotId, Guid ChartId, int Place)>> GetPopularityHistory(MixEnum mix,
        int snapshots, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var snapshotIds = await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.MixId == mixId && s.CompletedAt != null)
            .OrderByDescending(s => s.CompletedAt)
            .Take(snapshots)
            .Select(s => s.Id)
            .ToArrayAsync(ct);
        var ordering = snapshotIds.Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        return (await database.Set<OfficialChartPopularityEntity>()
                .Where(p => snapshotIds.Contains(p.SnapshotId))
                .Select(p => new { p.SnapshotId, p.ChartId, p.Place })
                .ToArrayAsync(ct))
            .OrderBy(p => ordering[p.SnapshotId])
            .Select(p => (p.SnapshotId, p.ChartId, p.Place))
            .ToArray();
    }

    public async Task<IReadOnlyList<(int SnapshotId, DateTimeOffset CompletedAt, decimal MinScore, int Count)>>
        GetBoardFloorHistory(MixEnum mix, string boardName, PlacementScope scope, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return (await Scoped(database.Set<OfficialLeaderboardPlacementEntity>(), scope)
                .Join(database.Set<OfficialLeaderboardEntity>()
                        .Where(b => b.MixId == mixId && b.LeaderboardType == LeaderboardTypes.Rating &&
                                    b.Name == boardName),
                    p => p.LeaderboardId, b => b.Id, (p, _) => p)
                .Join(database.Set<OfficialLeaderboardSnapshotEntity>().Where(s => s.CompletedAt != null),
                    p => p.SnapshotId, s => s.Id, (p, s) => new { p, CompletedAt = s.CompletedAt!.Value })
                .GroupBy(ps => new { ps.p.SnapshotId, ps.CompletedAt })
                .Select(g => new
                {
                    g.Key.SnapshotId,
                    g.Key.CompletedAt,
                    MinScore = g.Min(ps => ps.p.Score),
                    Count = g.Count()
                })
                .OrderBy(g => g.CompletedAt)
                .ToArrayAsync(ct))
            .Select(g => (g.SnapshotId, g.CompletedAt, g.MinScore, g.Count))
            .ToArray();
    }

    public async Task UpsertMissingCharts(MixEnum mix, IReadOnlyCollection<MissingChartSighting> sightings,
        DateTimeOffset seenAt, CancellationToken ct)
    {
        if (sightings.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var existing = await database.Set<OfficialMissingChartEntity>()
            .Where(m => m.MixId == mixId)
            .ToArrayAsync(ct);
        var known = existing.ToDictionary(
            m => (m.SongName, m.ChartType, m.Level),
            m => m);
        foreach (var sighting in sightings
                     .GroupBy(s => (s.SongName, s.ChartType, s.Level))
                     .Select(g => g.First()))
            if (known.TryGetValue((sighting.SongName, sighting.ChartType, sighting.Level), out var entity))
            {
                entity.LastIdentified = seenAt;
            }
            else
            {
                await database.Set<OfficialMissingChartEntity>().AddAsync(new OfficialMissingChartEntity
                {
                    MixId = mixId,
                    SongName = sighting.SongName,
                    ChartType = sighting.ChartType,
                    Level = sighting.Level,
                    FirstIdentified = seenAt,
                    LastIdentified = seenAt
                }, ct);
            }

        await database.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MissingChartRow>> GetMissingCharts(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await database.Set<OfficialMissingChartEntity>()
            .Where(m => m.MixId == mixId)
            .OrderByDescending(m => m.LastIdentified).ThenBy(m => m.SongName)
            .Select(m => new MissingChartRow(m.Id, m.SongName, m.ChartType, m.Level, m.FirstIdentified,
                m.LastIdentified))
            .ToArrayAsync(ct);
    }

    public async Task DeleteMissingChart(int id, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialMissingChartEntity>().Where(m => m.Id == id).ExecuteDeleteAsync(ct);
    }

    private static SnapshotRun ToRun(OfficialLeaderboardSnapshotEntity entity)
    {
        return new SnapshotRun(entity.Id, entity.StartedAt, entity.CompletedAt, entity.IsBaseline, entity.Stage,
            entity.BoardsExpected, entity.BoardsWritten, entity.BoardsSkipped, entity.Error);
    }

    private static PlayerDimension ToPlayer(OfficialPlayerEntity entity)
    {
        return new PlayerDimension(entity.Id, entity.Username,
            entity.AvatarUrl == null ? null : new Uri(entity.AvatarUrl, UriKind.Absolute), entity.UserId,
            entity.LastSeenAt);
    }
}
