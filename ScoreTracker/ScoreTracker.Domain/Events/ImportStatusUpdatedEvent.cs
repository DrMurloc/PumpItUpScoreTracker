using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.Domain.Events
{
    [ExcludeFromCodeCoverage]
    public sealed record ImportStatusUpdatedEvent(Guid UserId, string Status,
        IEnumerable<RecordedPhoenixScore> Scores, MixEnum Mix) : INotification
    {
    }
}
