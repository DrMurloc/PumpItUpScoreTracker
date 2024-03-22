using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.Events
{
    public sealed record PlayerStatsUpdatedEvent(Guid UserId, PlayerStatsRecord NewStats) : INotification
    {
    }
}
