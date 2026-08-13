using Microsoft.EntityFrameworkCore;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.ChartIntelligence.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.ChartIntelligence.Infrastructure;

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
        var chosen = new List<Type>();
        if (items.HasFlag(ContributionDeletionItems.ChartDifficultyRatings))
            chosen.Add(typeof(UserChartDifficultyRatingEntity));
        if (items.HasFlag(ContributionDeletionItems.ChartPreferenceRatings))
            chosen.Add(typeof(UserPreferenceRatingEntity));
        if (items.HasFlag(ContributionDeletionItems.CoOpRatings)) chosen.Add(typeof(UserCoOpRatingEntity));
        return chosen.Count == 0
            ? Task.CompletedTask
            : UserDataPurge.DeleteAll(_factory, chosen, userId, cancellationToken);
    }
}
