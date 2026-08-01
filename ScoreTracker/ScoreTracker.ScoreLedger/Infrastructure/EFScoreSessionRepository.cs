using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
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

    private static ScoreSessionRecord Map(ScoreSessionEntity e)
    {
        return new ScoreSessionRecord(e.Id, e.UserId, MixIds.ToEnum(e.MixId), e.Source, e.AccountTag, e.CardId,
            e.StartedAt, e.LastActivityAt, e.ScoreCount, e.NewCount, e.UpscoreCount);
    }
}
