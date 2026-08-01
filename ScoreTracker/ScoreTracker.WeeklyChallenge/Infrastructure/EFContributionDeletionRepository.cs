using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;
using ScoreTracker.WeeklyChallenge.Domain;
using ScoreTracker.WeeklyChallenge.Infrastructure.Entities;

namespace ScoreTracker.WeeklyChallenge.Infrastructure;

internal sealed class EFContributionDeletionRepository : IContributionDeletionRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFContributionDeletionRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public Task Delete(Guid userId, ContributionDeletionItems items,
        CancellationToken cancellationToken = default)
    {
        // Entries and placings go together: a placing without its entry is a standing on a
        // board nobody can trace back to a score.
        if (!items.HasFlag(ContributionDeletionItems.WeeklyAndDailyStep)) return Task.CompletedTask;
        return UserDataPurge.DeleteAll(_factory, new[]
        {
            typeof(WeeklyUserEntry),
            typeof(UserWeeklyPlacingEntity),
            typeof(DailyStepEntryEntity),
            typeof(UserDailyStepPlacingEntity)
        }, userId, cancellationToken);
    }
}
