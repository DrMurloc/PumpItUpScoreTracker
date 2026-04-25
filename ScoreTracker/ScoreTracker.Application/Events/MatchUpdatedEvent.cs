using MediatR;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record MatchUpdatedEvent(Guid TournamentId, MatchView NewState) : INotification
    {
    }
}
