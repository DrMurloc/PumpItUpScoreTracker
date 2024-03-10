using MediatR;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.Events
{
    public sealed record ImportStatusUpdated(Guid UserId, string Status,
        IEnumerable<RecordedPhoenixScore> Scores) : INotification
    {
    }
}
