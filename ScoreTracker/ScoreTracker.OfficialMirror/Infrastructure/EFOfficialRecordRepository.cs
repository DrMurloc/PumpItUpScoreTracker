using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.OfficialMirror.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Infrastructure;

internal sealed class EFOfficialRecordRepository : IOfficialRecordRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFOfficialRecordRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<BoardRecordRow>> GetBoardRecords(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        // Board records carry no mix column — scope through the board dimension.
        return await database.Set<OfficialBoardRecordEntity>()
            .Join(database.Set<OfficialLeaderboardEntity>().Where(b => b.MixId == mixId),
                r => r.LeaderboardId, b => b.Id,
                (r, _) => new BoardRecordRow(r.LeaderboardId, r.HighScore, r.AchievedSnapshotId))
            .ToArrayAsync(ct);
    }

    public async Task UpsertBoardRecords(IReadOnlyCollection<BoardRecordRow> records, CancellationToken ct)
    {
        if (records.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(ct);
        var ids = records.Select(r => r.LeaderboardId).ToArray();
        var existing = await database.Set<OfficialBoardRecordEntity>()
            .Where(r => ids.Contains(r.LeaderboardId))
            .ToDictionaryAsync(r => r.LeaderboardId, ct);
        foreach (var record in records)
            if (existing.TryGetValue(record.LeaderboardId, out var entity))
            {
                entity.HighScore = record.HighScore;
                entity.AchievedSnapshotId = record.AchievedSnapshotId;
            }
            else
            {
                await database.Set<OfficialBoardRecordEntity>().AddAsync(new OfficialBoardRecordEntity
                {
                    LeaderboardId = record.LeaderboardId,
                    HighScore = record.HighScore,
                    AchievedSnapshotId = record.AchievedSnapshotId
                }, ct);
            }

        await database.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FolderRecordRow>> GetFolderRecords(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        return await database.Set<OfficialFolderRecordEntity>()
            .Where(r => r.MixId == mixId)
            .Select(r => new FolderRecordRow(r.ChartType, r.Level, r.HighScore, r.AchievedSnapshotId))
            .ToArrayAsync(ct);
    }

    public async Task UpsertFolderRecords(MixEnum mix, IReadOnlyCollection<FolderRecordRow> records,
        CancellationToken ct)
    {
        if (records.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var existing = await database.Set<OfficialFolderRecordEntity>()
            .Where(r => r.MixId == mixId)
            .ToDictionaryAsync(r => (r.ChartType, r.Level), ct);
        foreach (var record in records)
            if (existing.TryGetValue((record.ChartType, record.Level), out var entity))
            {
                entity.HighScore = record.HighScore;
                entity.AchievedSnapshotId = record.AchievedSnapshotId;
            }
            else
            {
                await database.Set<OfficialFolderRecordEntity>().AddAsync(new OfficialFolderRecordEntity
                {
                    MixId = mixId,
                    ChartType = record.ChartType,
                    Level = record.Level,
                    HighScore = record.HighScore,
                    AchievedSnapshotId = record.AchievedSnapshotId
                }, ct);
            }

        await database.SaveChangesAsync(ct);
    }

    public async Task ResetRecords(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        var boardIds = database.Set<OfficialLeaderboardEntity>()
            .Where(b => b.MixId == mixId).Select(b => b.Id);
        await database.Set<OfficialBoardRecordEntity>()
            .Where(r => boardIds.Contains(r.LeaderboardId)).ExecuteDeleteAsync(ct);
        await database.Set<OfficialFolderRecordEntity>()
            .Where(r => r.MixId == mixId).ExecuteDeleteAsync(ct);
    }

    public async Task WriteHighlights(int snapshotId, MixEnum mix, IReadOnlyCollection<HighlightRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        await database.Set<OfficialWeeklyHighlightEntity>().AddRangeAsync(rows.Select(r =>
            new OfficialWeeklyHighlightEntity
            {
                SnapshotId = snapshotId,
                MixId = mixId,
                Kind = r.Kind,
                SortOrder = r.SortOrder,
                PlayerId = r.PlayerId,
                DethronedPlayerId = r.DethronedPlayerId,
                LeaderboardId = r.LeaderboardId,
                ChartId = r.ChartId,
                ChartType = r.ChartType,
                Level = r.Level,
                GradeBand = r.GradeBand,
                Score = r.Score,
                PrevValue = r.PrevValue,
                NewValue = r.NewValue
            }), ct);
        await database.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<HighlightRow>> GetHighlights(int snapshotId, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        return await database.Set<OfficialWeeklyHighlightEntity>()
            .Where(h => h.SnapshotId == snapshotId)
            .OrderBy(h => h.Kind).ThenBy(h => h.SortOrder)
            .Select(h => new HighlightRow(h.Kind, h.SortOrder, h.PlayerId, h.DethronedPlayerId, h.LeaderboardId,
                h.ChartId, h.ChartType, h.Level, h.GradeBand, h.Score, h.PrevValue, h.NewValue))
            .ToArrayAsync(ct);
    }

    public async Task DeleteHighlights(MixEnum mix, CancellationToken ct)
    {
        await using var database = await _factory.CreateDbContextAsync(ct);
        var mixId = MixIds.For(mix);
        await database.Set<OfficialWeeklyHighlightEntity>()
            .Where(h => h.MixId == mixId).ExecuteDeleteAsync(ct);
    }
}
