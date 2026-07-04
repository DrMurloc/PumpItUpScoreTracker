using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application
{
    internal sealed class
        GetPhoenixScoresForChartHandler : IRequestHandler<GetPhoenixScoresForChartQuery, IEnumerable<UserPhoenixScore>>
    {
        private readonly IPhoenixRecordRepository _records;

        public GetPhoenixScoresForChartHandler(IPhoenixRecordRepository records)
        {
            _records = records;
        }

        public async Task<IEnumerable<UserPhoenixScore>> Handle(GetPhoenixScoresForChartQuery request,
            CancellationToken cancellationToken)
            => await _records.GetRecordedUserScores(request.Mix, request.ChartId, cancellationToken);
    }
}