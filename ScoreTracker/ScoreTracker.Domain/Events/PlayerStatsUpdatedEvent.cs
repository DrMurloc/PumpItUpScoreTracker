using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record PlayerStatsUpdatedEvent(Guid UserId, PlayerStatsRecord NewStats) : INotification
    {
    }
}
