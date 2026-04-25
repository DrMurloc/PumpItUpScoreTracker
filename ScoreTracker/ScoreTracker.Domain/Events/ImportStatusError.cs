using MediatR;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record ImportStatusError(Guid UserId, string Error) : INotification

    {
    }
}
