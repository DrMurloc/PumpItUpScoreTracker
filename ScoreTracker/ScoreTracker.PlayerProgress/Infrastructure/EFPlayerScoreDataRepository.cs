using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFPlayerScoreDataRepository : IPlayerScoreDataRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFPlayerScoreDataRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task DeleteHistory(Guid userId, MixEnum? mix, CancellationToken cancellationToken = default)
    {
        var mixId = MixIdOrNull(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<PlayerHistoryEntity>()
            .Where(e => e.UserId == userId && (mixId == null || e.MixId == mixId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteHighlights(Guid userId, MixEnum? mix, CancellationToken cancellationToken = default)
    {
        var mixId = MixIdOrNull(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ScoreHighlightEntity>()
            .Where(e => e.UserId == userId && (mixId == null || e.MixId == mixId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteMilestones(Guid userId, MixEnum? mix, CancellationToken cancellationToken = default)
    {
        var mixId = MixIdOrNull(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<PlayerMilestoneEntity>()
            .Where(e => e.UserId == userId && (mixId == null || e.MixId == mixId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteForSession(Guid userId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<ScoreHighlightEntity>()
            .Where(e => e.UserId == userId && e.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
        await database.Set<PlayerMilestoneEntity>()
            .Where(e => e.UserId == userId && e.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static Guid? MixIdOrNull(MixEnum? mix)
    {
        return mix == null ? null : MixIds.For(mix.Value);
    }
}
