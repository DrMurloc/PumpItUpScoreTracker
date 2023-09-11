using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetPhoenixScoresForChartQuery(Guid ChartId) : IRequest<IEnumerable<UserPhoenixScore>>
    {
    }
}