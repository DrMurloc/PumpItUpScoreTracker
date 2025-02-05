using MediatR;

namespace ScoreTracker.Domain.Events
{
    public sealed record ImportStatusError(Guid UserId, string Error) : INotification

    {
    }
}
