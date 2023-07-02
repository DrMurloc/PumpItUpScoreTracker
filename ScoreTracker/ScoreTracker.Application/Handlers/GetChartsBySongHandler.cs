using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetChartsBySongHandler : IRequestHandler<GetChartsBySongQuery, IEnumerable<Chart>>
{
    private readonly IChartRepository _charts;

    public GetChartsBySongHandler(IChartRepository charts)
    {
        _charts = charts;
    }

    public async Task<IEnumerable<Chart>> Handle(GetChartsBySongQuery request, CancellationToken cancellationToken)
    {
        return await _charts.GetChartsForSong(request.Mix, request.SongName, cancellationToken);
    }
}