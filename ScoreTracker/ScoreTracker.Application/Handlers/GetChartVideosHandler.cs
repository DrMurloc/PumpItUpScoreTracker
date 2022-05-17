using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetChartVideosHandler : IRequestHandler<GetChartVideosQuery, IEnumerable<ChartVideoInformation>>
{
    private readonly IChartRepository _chartRepository;

    public GetChartVideosHandler(IChartRepository chartRepository)
    {
        _chartRepository = chartRepository;
    }

    public async Task<IEnumerable<ChartVideoInformation>> Handle(GetChartVideosQuery request,
        CancellationToken cancellationToken)
    {
        return await _chartRepository.GetChartVideoInformation(request.ChartIds, cancellationToken);
    }
}