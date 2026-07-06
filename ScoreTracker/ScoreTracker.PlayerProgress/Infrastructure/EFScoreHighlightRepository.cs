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
            }
            else
            {
                await database.AddAsync(new ScoreHighlightEntity
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
                }, cancellationToken);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ScoreHighlightRecord>> GetHighlights(MixEnum mix, Guid userId,
        DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreHighlightEntity>()
                .Where(e => e.UserId == userId && e.MixId == mixId && e.OccurredAt >= since && e.OccurredAt <= until)
                .ToArrayAsync(cancellationToken))
            .Select(e => new ScoreHighlightRecord(e.ChartId, e.SessionId, e.OccurredAt, (HighlightFlags)e.Flags,
                e.Level, e.ScoringLevel));
    }
}
