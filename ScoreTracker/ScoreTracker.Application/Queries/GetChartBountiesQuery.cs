using MediatR;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.Application.Queries
{
    [ExcludeFromCodeCoverage]
    public sealed class GetChartBountiesQuery : IRequest<IEnumerable<ChartBounty>>
    {
    }
}
