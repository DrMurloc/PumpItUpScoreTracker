using MediatR;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetTop50ForPlayerQuery
        (Guid UserId, ChartType? ChartType, int Count = 50) : IQuery<IEnumerable<RecordedPhoenixScore>>
    {
    }
}
