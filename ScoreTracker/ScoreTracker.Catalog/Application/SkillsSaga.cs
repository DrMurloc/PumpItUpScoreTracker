using MediatR;
using ScoreTracker.Catalog.Contracts.Commands;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Application;

internal sealed class SkillsSaga : IRequestHandler<GetChartSkillsQuery, IEnumerable<ChartSkillsRecord>>,
    IRequestHandler<UpdateChartSkillCommand>
{
    private readonly IChartRepository _charts;

    public SkillsSaga(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<IEnumerable<ChartSkillsRecord>> Handle(GetChartSkillsQuery request,
        CancellationToken cancellationToken)
    {
        return await _charts.GetChartSkills(cancellationToken);
    }

    public async Task Handle(UpdateChartSkillCommand request, CancellationToken cancellationToken)
    {
        await _charts.SaveChartSkills(request.Skills, cancellationToken);
    }
}