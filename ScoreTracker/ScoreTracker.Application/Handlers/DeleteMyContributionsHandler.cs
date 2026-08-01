using MassTransit;
using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Handlers;

public sealed class DeleteMyContributionsHandler(IBus bus) : IRequestHandler<DeleteMyContributionsCommand>
{
    public Task Handle(DeleteMyContributionsCommand request, CancellationToken cancellationToken)
    {
        if (request.Items == ContributionDeletionItems.None) return Task.CompletedTask;
        return bus.Publish(new ContributionsDeletionRequestedEvent(request.UserId, request.Items),
            cancellationToken);
    }
}
