using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.Communities.Domain;

namespace ScoreTracker.Communities.Application;

/// <summary>
///     Removes this vertical's share of a player's contributions when they ask for them to go.
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
