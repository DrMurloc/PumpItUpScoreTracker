using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Application.Handlers;

public sealed class GetChartFolderByNameHandler : IRequestHandler<GetChartFolderByNameQuery, ChartFolder>
{
    private readonly IChartRepository _chartRepository;

    public GetChartFolderByNameHandler(IChartRepository chartRepository)
    {
        _chartRepository = chartRepository;
    }

    public async Task<ChartFolder> Handle(GetChartFolderByNameQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<Chart> charts;
        var songOrders = await _chartRepository.GetSongOrder(cancellationToken);
        ChartType? type = null;
        DifficultyLevel? difficultyLevel = null;

        if (DifficultyLevel.TryParseShortHand(request.Name, out var chartType, out var level))
        {
            type = chartType;
            difficultyLevel = level;
            var chartTypes = chartType is ChartType.Single or ChartType.SinglePerformance
                ? new[] { ChartType.Single, ChartType.SinglePerformance }
                : new[] { ChartType.Double, ChartType.DoublePerformance };

            charts = await _chartRepository.GetCharts(new[] { level }, chartTypes, null,
                cancellationToken);
        }
        else
        {
            charts = await _chartRepository.GetCharts(null, null, request.Name, cancellationToken);
        }


        return new ChartFolder(request.Name,
            charts.OrderBy(c => songOrders[c.Song.Name]).ThenBy(c => c.Type).ThenBy(c => c.Level), type,
            difficultyLevel);
    }
}