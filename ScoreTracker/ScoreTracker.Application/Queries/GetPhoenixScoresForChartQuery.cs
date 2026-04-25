using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed record GetPhoenixScoresForChartQuery(Guid ChartId) : IRequest<IEnumerable<UserPhoenixScore>>
    {
    }
}
