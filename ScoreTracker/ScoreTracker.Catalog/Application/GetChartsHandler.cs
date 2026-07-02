using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Application;

internal sealed class GetChartsHandler : IRequestHandler<GetChartsQuery, IEnumerable<Chart>>
{
    private readonly IChartRepository _charts;

    public GetChartsHandler(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<IEnumerable<Chart>> Handle(GetChartsQuery request, CancellationToken cancellationToken)
    {
        return await _charts.GetCharts(request.Mix, request.Level, request.Type, request.ChartIds, cancellationToken);
    }
}