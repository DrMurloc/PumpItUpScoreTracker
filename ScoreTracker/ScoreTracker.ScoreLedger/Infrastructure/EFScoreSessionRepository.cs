using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.ScoreLedger.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Infrastructure;

internal sealed class EFScoreSessionRepository : IScoreSessionRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFScoreSessionRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Open(Guid id, Guid userId, MixEnum mix, string source, string? accountTag, string? cardId,
        DateTimeOffset startedAt, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // The accumulator can hand the same id to two concurrent submissions; whoever loses the
        // race must not rewrite the start time.
        if (await database.Set<ScoreSessionEntity>().AnyAsync(s => s.Id == id, cancellationToken)) return;

        database.Set<ScoreSessionEntity>().Add(new ScoreSessionEntity
        {
            Id = id,
            UserId = userId,
            MixId = MixIds.For(mix),
            Source = source,
            AccountTag = accountTag,
            CardId = cardId,
            StartedAt = startedAt,
            LastActivityAt = startedAt
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task Touch(Guid id, DateTimeOffset at, int newCount, int upscoreCount,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ScoreSessionEntity>()
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.LastActivityAt, at)
                .SetProperty(s => s.NewCount, s => s.NewCount + newCount)
                .SetProperty(s => s.UpscoreCount, s => s.UpscoreCount + upscoreCount)
                .SetProperty(s => s.ScoreCount, s => s.ScoreCount + newCount + upscoreCount), cancellationToken);
    }

    public async Task SetCounts(Guid id, DateTimeOffset at, int newCount, int upscoreCount,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ScoreSessionEntity>()
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.LastActivityAt, at)
                .SetProperty(s => s.NewCount, newCount)
                .SetProperty(s => s.UpscoreCount, upscoreCount)
                .SetProperty(s => s.ScoreCount, newCount + upscoreCount), cancellationToken);
    }

    public async Task MarkProcessed(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // Only stamps an UNprocessed row. A session drains as several batches and each one
        // publishes its own capture, so this runs repeatedly for a live session; the first
        // stamp is the one that means "the derived work reached the end at least once".
        await database.Set<ScoreSessionEntity>()
            .Where(s => s.Id == id && s.ProcessedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.ProcessedAt, at), cancellationToken);
    }

    public async Task<IReadOnlyList<ScoreSessionRecord>> ListUnprocessed(
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreSessionEntity>()
                .Where(s => s.ProcessedAt == null)
                .OrderBy(s => s.StartedAt)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task<ScoreSessionRecord?> Get(Guid id, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<ScoreSessionEntity>().FirstOrDefaultAsync(s => s.Id == id,
            cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ScoreSessionRecord>> ListFor(Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreSessionEntity>()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.StartedAt)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ScoreSessionEntity>().Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<OpenSitting?> GetOpenSitting(Guid userId, MixEnum mix, DateTimeOffset activeSince,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // A replayed sitting carries its counts before capture stamps it processed, and its replay
        // moved LastActivityAt to the replay's own time — so without the counts test a sitting being
        // announced would look open, and a play joining it would never be announced itself.
        var sitting = await database.Set<ScoreSessionEntity>()
            .Where(s => s.UserId == userId && s.MixId == mixId && s.ProcessedAt == null
                        && s.NewCount == 0 && s.UpscoreCount == 0
                        && s.Source.StartsWith(ScoreJournalEntry.PlaysApiSourcePrefix)
                        && s.LastActivityAt >= activeSince)
            .OrderByDescending(s => s.LastActivityAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (sitting is null) return null;

        var plays = database.Set<ScoreEventJournalEntity>()
            .Where(j => j.UserId == userId && j.SessionId == sitting.Id)
            .Select(j => (DateTimeOffset?)j.OccurredAt);
        var first = await plays.MinAsync(cancellationToken);
        var last = await plays.MaxAsync(cancellationToken);
        return new OpenSitting(sitting.Id, first ?? sitting.StartedAt, last ?? sitting.StartedAt);
    }

    public async Task TouchArrival(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ScoreSessionEntity>()
            .Where(s => s.Id == id && s.LastActivityAt < at)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.LastActivityAt, at), cancellationToken);
    }

    public async Task<DateTimeOffset?> GetLastPlayedAt(Guid userId, Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<ScoreEventJournalEntity>()
            .Where(j => j.UserId == userId && j.SessionId == sessionId)
            .Select(j => (DateTimeOffset?)j.OccurredAt)
            .MaxAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScoreSessionRecord>> ListOverdueSittings(DateTimeOffset quietSince, int take,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreSessionEntity>()
                .Where(s => s.ProcessedAt == null && s.NewCount == 0 && s.UpscoreCount == 0
                            && s.Source.StartsWith(ScoreJournalEntry.PlaysApiSourcePrefix)
                            && s.LastActivityAt <= quietSince)
                .OrderBy(s => s.LastActivityAt)
                .Take(take)
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    private static ScoreSessionRecord Map(ScoreSessionEntity e)
    {
        return new ScoreSessionRecord(e.Id, e.UserId, MixIds.ToEnum(e.MixId), e.Source, e.AccountTag, e.CardId,
            e.StartedAt, e.LastActivityAt, e.ScoreCount, e.NewCount, e.UpscoreCount, e.ProcessedAt);
    }

    public async Task<IReadOnlyList<ScoreSessionRecord>> ListLatestPerUser(
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<ScoreSessionEntity>()
                .GroupBy(s => s.UserId)
                .Select(g => g.OrderByDescending(s => s.LastActivityAt).First())
                .ToArrayAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }
}
