using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetTop50CompetitiveQuery
        (Guid UserId, ChartType? ChartType) : IQuery<IEnumerable<RecordedPhoenixScore>>
    {
    }
}
