using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class
        GetPhoenixScoresForChartHandler : IRequestHandler<GetPhoenixScoresForChartQuery, IEnumerable<UserPhoenixScore>>
    {
        private readonly IPhoenixRecordRepository _records;

        public GetPhoenixScoresForChartHandler(IPhoenixRecordRepository records)
        {
            _records = records;
        }

        public async Task<IEnumerable<UserPhoenixScore>> Handle(GetPhoenixScoresForChartQuery request,
            CancellationToken cancellationToken)
            => await _records.GetRecordedUserScores(request.ChartId, cancellationToken);
    }
}