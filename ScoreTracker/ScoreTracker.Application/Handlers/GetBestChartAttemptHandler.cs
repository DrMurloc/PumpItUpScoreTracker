using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetBestChartAttemptHandler : IRequestHandler<GetBestChartAttemptQuery, BestChartAttempt>
{
    private readonly IChartAttemptRepository _chartAttempts;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _user;

    public GetBestChartAttemptHandler(ICurrentUserAccessor user,
        IChartAttemptRepository chartAttempts,
        IChartRepository charts)
    {
        _user = user;
        _chartAttempts = chartAttempts;
        _charts = charts;
    }

    public async Task<BestChartAttempt> Handle(GetBestChartAttemptQuery request, CancellationToken cancellationToken)
    {
        var chart = await _charts.GetChart(request.SongName, request.ChartType, request.Level, cancellationToken);
        var bestAttempt = await _chartAttempts.GetBestAttempt(_user.UserId, chart, cancellationToken);
        return new BestChartAttempt(chart, bestAttempt);
    }
}