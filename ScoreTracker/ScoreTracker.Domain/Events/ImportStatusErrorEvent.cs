using MediatR;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record ImportStatusErrorEvent(Guid UserId, string Error) : INotification

    {
    }
}
