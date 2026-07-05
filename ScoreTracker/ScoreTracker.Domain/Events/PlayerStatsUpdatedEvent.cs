using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record PlayerStatsUpdatedEvent(Guid UserId, PlayerStatsRecord NewStats, MixEnum Mix) : INotification
    {
    }
}
