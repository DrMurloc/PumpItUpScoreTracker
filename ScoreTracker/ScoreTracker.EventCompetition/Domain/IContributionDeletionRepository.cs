using ScoreTracker.Domain.Records;

namespace ScoreTracker.EventCompetition.Domain;

internal interface IContributionDeletionRepository
{
    Task Delete(Guid userId, ContributionDeletionItems items, CancellationToken cancellationToken = default);
}
