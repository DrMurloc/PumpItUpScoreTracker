using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.PersonalProgress.Queries
{
    public sealed record GetTop50CompetitiveQuery
        (Guid UserId, ChartType? ChartType) : IRequest<IEnumerable<RecordedPhoenixScore>>
    {
    }
}
