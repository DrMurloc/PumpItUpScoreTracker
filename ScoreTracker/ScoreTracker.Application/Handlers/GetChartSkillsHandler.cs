using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers
{
    public sealed class GetChartSkillsHandler : IRequestHandler<GetChartSkillsQuery, IEnumerable<ChartSkillsRecord>>,
        IRequestHandler<GetSkillsQuery, IEnumerable<SkillRecord>>
    {
        private readonly IChartRepository _charts;

        public GetChartSkillsHandler(IChartRepository charts)
        {
            _charts = charts;
        }

        public async Task<IEnumerable<ChartSkillsRecord>> Handle(GetChartSkillsQuery request,
            CancellationToken cancellationToken)
        {
            return await _charts.GetChartSkills(cancellationToken);
        }

        public async Task<IEnumerable<SkillRecord>> Handle(GetSkillsQuery request, CancellationToken cancellationToken)
        {
            return await _charts.GetSkills(cancellationToken);
        }
    }
}
