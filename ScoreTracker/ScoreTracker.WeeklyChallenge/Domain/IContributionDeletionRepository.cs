using ScoreTracker.Domain.Records;

namespace ScoreTracker.WeeklyChallenge.Domain;

internal interface IContributionDeletionRepository
{
    Task Delete(Guid userId, ContributionDeletionItems items, CancellationToken cancellationToken = default);
}
