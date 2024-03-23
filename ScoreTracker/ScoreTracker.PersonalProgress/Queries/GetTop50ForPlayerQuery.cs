using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.PersonalProgress.Queries
{
    public sealed record GetTop50ForPlayerQuery
        (Guid UserId, ChartType? ChartType) : IRequest<IEnumerable<RecordedPhoenixScore>>
    {
    }
}
