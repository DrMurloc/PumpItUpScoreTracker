using MediatR;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record ImportStatusErrorEvent(Guid UserId, string Error, MixEnum Mix) : INotification

    {
    }
}
