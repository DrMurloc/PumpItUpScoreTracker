using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class
        GetAllChartScoreAggregatesHandler : IRequestHandler<GetAllChartScoreAggregatesQuery,
            IEnumerable<ChartScoreAggregate>>
    {
        private IPhoenixRecordRepository _records;

        public GetAllChartScoreAggregatesHandler(IPhoenixRecordRepository records)
        {
            _records = records;
        }

        public async Task<IEnumerable<ChartScoreAggregate>> Handle(GetAllChartScoreAggregatesQuery request,
            CancellationToken cancellationToken)
        {
            return await _records.GetAllChartScoreAggregates(cancellationToken);
        }
    }
}