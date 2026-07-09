using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFScoreHighlightRepository : IScoreHighlightRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFScoreHighlightRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task UpsertFlags(MixEnum mix, Guid userId, IEnumerable<ScoreHighlightWrite> highlights,
        CancellationToken cancellationToken)
    {
        var writes = highlights.Where(h => h.Flags != HighlightFlags.None).ToArray();
        if (!writes.Any()) return;

        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var chartIds = writes.Select(w => w.ChartId).ToArray();
        var sessionIds = writes.Select(w => w.SessionId).Distinct().ToArray();
        var existing = await database.Set<ScoreHighlightEntity>()
            .Where(e => e.UserId == userId && e.MixId == mixId && chartIds.Contains(e.ChartId)
                        && sessionIds.Contains(e.SessionId))
            .ToArrayAsync(cancellationToken);

        foreach (var write in writes)
        {
            // Capture and the rating saga's competitive-improver pass race in either
            // order — OR-ing into whichever row exists keeps both writers additive.
            // (A simultaneous first-write can still duplicate a row; readers group by
            // chart, so a duplicate is cosmetic rather than corrupting.)
            var row = existing.FirstOrDefault(e => e.ChartId == write.ChartId && e.SessionId == write.SessionId);
            if (row != null)
            {
                row.Flags |= (int)write.Flags;
                if (row.ScoringLevel == null && write.ScoringLevel != null) row.ScoringLevel = write.ScoringLevel;
                MergeDetail(row, write.Detail);
            }
            else
            {
                var entity = new ScoreHighlightEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    MixId = mixId,
                    ChartId = write.ChartId,
                    SessionId = write.SessionId,
                    OccurredAt = write.OccurredAt,
                    Flags = (int)write.Flags,
                    Level = write.Level,
                    ScoringLevel = write.ScoringLevel
                };
                MergeDetail(entity, write.Detail);
                await database.AddAsync(entity, cancellationToken);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    // Detail is set once by capture; the improver pass carries none, so fill only nulls.
    private static void MergeDetail(ScoreHighlightEntity row, HighlightDetail? detail)
    {
        if (detail == null) return;
        row.PumbilityRank ??= detail.PumbilityRank;
        row.FolderDebutOrdinal ??= detail.FolderDebutOrdinal;
        row.PeerCount ??= detail.PeerCount;
        row.PeerBetterCount ??= detail.PeerBetterCount;
        row.PeerPgCount ??= detail.PeerPgCount;
        row.SkillTitleName ??= detail.SkillTitleName;
        row.SkillTitleScore ??= detail.SkillTitleScore;
        row.SkillTitleThreshold ??= detail.SkillTitleThreshold;
    }

    private static ScoreHighlightRecord ToRecord(ScoreHighlightEntity e)
    {
        return new ScoreHighlightRecord(e.ChartId, e.SessionId, e.OccurredAt, (HighlightFlags)e.Flags, e.Level,
            e.ScoringLevel, new HighlightDetail(e.PumbilityRank, e.FolderDebutOrdinal, e.PeerCount, e.PeerBetterCount,
                e.PeerPgCount, e.SkillTitleName, e.SkillTitleScore, e.SkillTitleThreshold));
    }

    public async Task<IEnumerable<ScoreHighlightRecord>> GetHighlights(MixEnum mix, Guid userId,
        DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreHighlightEntity>()
                .Where(e => e.UserId == userId && e.MixId == mixId && e.OccurredAt >= since && e.OccurredAt <= until)
                .ToArrayAsync(cancellationToken))
            .Select(ToRecord);
    }

    public async Task<IEnumerable<ScoreHighlightRecord>> GetHighlightsBySessions(Guid userId,
        IEnumerable<Guid> sessionIds, CancellationToken cancellationToken)
    {
        var ids = sessionIds.Distinct().Select(s => (Guid?)s).ToArray();
        if (ids.Length == 0) return Array.Empty<ScoreHighlightRecord>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreHighlightEntity>()
                .Where(e => e.UserId == userId && ids.Contains(e.SessionId))
                .ToArrayAsync(cancellationToken))
            .Select(ToRecord);
    }
}
