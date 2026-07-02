using MediatR;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Catalog.Application;

internal sealed class GetChartHandler : IRequestHandler<GetChartQuery, Chart?>
{
    private readonly IChartRepository _charts;

    public GetChartHandler(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<Chart?> Handle(GetChartQuery request, CancellationToken cancellationToken)
    {
        return (await _charts.GetChartsForSong(request.Mix, request.SongName, cancellationToken)).FirstOrDefault(
            c => c.Type == request.Type && c.Level == request.Level);
    }
}