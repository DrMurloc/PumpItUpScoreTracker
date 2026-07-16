using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
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
            IsBaseline = isBaseline,
            Stage = "Created"
        };
        await database.Set<OfficialLeaderboardSnapshotEntity>().AddAsync(entity, ct);
        await database.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateProgress(int snapshotId, string stage, int boardsExpected, int boardsWritten,
        int boardsSkipped, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialLeaderboardSnapshotEntity>()
            .Where(s => s.Id == snapshotId)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Stage, stage)
                .SetProperty(s => s.BoardsExpected, boardsExpected)
                .SetProperty(s => s.BoardsWritten, boardsWritten)
                .SetProperty(s => s.BoardsSkipped, boardsSkipped), ct);
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

    public async Task<bool> HasUnsealedRunSince(MixEnum mix, DateTimeOffset since, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await database.Set<OfficialLeaderboardSnapshotEntity>()
            .AnyAsync(s => s.MixId == mixId && s.CompletedAt == null && s.StartedAt >= since, ct);
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

        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var result = new List<PlayerDimension>(players.Count);

        foreach (var chunk in players.Chunk(500))
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

    public async Task<IReadOnlyList<PlayerDimension>> GetPlayers(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return (await database.Set<OfficialPlayerEntity>()
                .Where(p => p.MixId == mixId)
                .ToArrayAsync(ct))
            .Select(ToPlayer).ToArray();
    }

    public async Task WritePlacements(int snapshotId, IReadOnlyCollection<PlacementRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(ct);
        await database.Set<OfficialLeaderboardPlacementEntity>().AddRangeAsync(rows.Select(r =>
            new OfficialLeaderboardPlacementEntity
            {
                SnapshotId = snapshotId,
                LeaderboardId = r.LeaderboardId,
                PlayerId = r.PlayerId,
                Place = r.Place,
                Score = r.Score
            }), ct);
        await database.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PlacementRow>> GetPlacements(int snapshotId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        return await database.Set<OfficialLeaderboardPlacementEntity>()
            .Where(p => p.SnapshotId == snapshotId)
            .Select(p => new PlacementRow(p.LeaderboardId, p.PlayerId, p.Place, p.Score))
            .ToArrayAsync(ct);
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

    public async Task<IReadOnlyList<(Guid ChartId, int Place)>> GetPopularity(int snapshotId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        return (await database.Set<OfficialChartPopularityEntity>()
                .Where(p => p.SnapshotId == snapshotId)
                .Select(p => new { p.ChartId, p.Place })
                .ToArrayAsync(ct))
            .Select(p => (p.ChartId, p.Place)).ToArray();
    }

    private static SnapshotRun ToRun(OfficialLeaderboardSnapshotEntity entity)
    {
        return new SnapshotRun(entity.Id, entity.StartedAt, entity.CompletedAt, entity.IsBaseline, entity.Stage,
            entity.BoardsExpected, entity.BoardsWritten, entity.BoardsSkipped, entity.Error);
    }

    private static PlayerDimension ToPlayer(OfficialPlayerEntity entity)
    {
        return new PlayerDimension(entity.Id, entity.Username,
            entity.AvatarUrl == null ? null : new Uri(entity.AvatarUrl, UriKind.Absolute), entity.UserId);
    }
}
