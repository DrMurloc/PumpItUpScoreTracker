using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Events
{
    public sealed record MatchUpdatedEvent(MatchView NewState) : INotification
    {
    }
}
