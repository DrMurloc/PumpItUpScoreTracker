using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetChartsHandler : IRequestHandler<GetChartsQuery, IEnumerable<Chart>>
{
    private readonly IChartRepository _charts;

    public GetChartsHandler(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<IEnumerable<Chart>> Handle(GetChartsQuery request, CancellationToken cancellationToken)
    {
        return await _charts.GetCharts(request.Mix, request.Level, request.Type, cancellationToken);
    }
}