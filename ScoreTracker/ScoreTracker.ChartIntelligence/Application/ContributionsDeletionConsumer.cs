using MassTransit;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     Removes a player's votes and ratings when they ask for their contributions to go.
///     Idempotent, like every other deletion consumer.
/// </summary>
internal sealed class ContributionsDeletionConsumer : IConsumer<ContributionsDeletionRequestedEvent>
{
    private readonly IContributionDeletionRepository _deletions;

    public ContributionsDeletionConsumer(IContributionDeletionRepository deletions)
    {
        _deletions = deletions;
    }

    public Task Consume(ConsumeContext<ContributionsDeletionRequestedEvent> context)
    {
        return _deletions.Delete(context.Message.UserId, context.Message.Items, context.CancellationToken);
    }
}
