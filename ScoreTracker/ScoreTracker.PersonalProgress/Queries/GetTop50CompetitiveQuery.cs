using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.PersonalProgress.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetTop50CompetitiveQuery
        (Guid UserId, ChartType? ChartType) : IQuery<IEnumerable<RecordedPhoenixScore>>
    {
    }
}
