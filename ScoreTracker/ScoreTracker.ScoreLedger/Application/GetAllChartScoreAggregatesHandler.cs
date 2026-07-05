using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application
{
    internal sealed class
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
            return await _records.GetAllChartScoreAggregates(request.Mix, cancellationToken);
        }
    }
}