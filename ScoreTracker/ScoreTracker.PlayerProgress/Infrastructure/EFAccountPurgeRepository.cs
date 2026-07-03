using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFAccountPurgeRepository : IAccountPurgeRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFAccountPurgeRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task DeleteAllForUser(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        await database.Set<UserTitleEntity>().Where(e => e.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await database.Set<SuggestionFeedbackEntity>().Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
