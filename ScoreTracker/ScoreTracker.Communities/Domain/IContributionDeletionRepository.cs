using ScoreTracker.Domain.Records;

namespace ScoreTracker.Communities.Domain;

internal interface IContributionDeletionRepository
{
    Task Delete(Guid userId, ContributionDeletionItems items, CancellationToken cancellationToken = default);
}
