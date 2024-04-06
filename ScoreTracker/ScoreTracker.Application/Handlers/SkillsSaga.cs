using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class SkillsSaga : IRequestHandler<GetChartSkillsQuery, IEnumerable<ChartSkillsRecord>>,
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