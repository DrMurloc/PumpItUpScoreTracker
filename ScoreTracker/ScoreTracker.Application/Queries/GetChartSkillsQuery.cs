using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    public sealed record GetChartSkillsQuery : IRequest<IEnumerable<ChartSkillsRecord>>
    {
    }
}
