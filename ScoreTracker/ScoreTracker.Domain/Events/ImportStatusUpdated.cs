using MediatR;

namespace ScoreTracker.Domain.Events
{
    public sealed record ImportStatusUpdated(Guid UserId, string Status) : INotification
    {
    }
}
