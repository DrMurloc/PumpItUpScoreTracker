using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetXXBestChartAttemptHandler : IRequestHandler<GetXXBestChartAttemptQuery, BestXXChartAttempt>
{
    private readonly IXXChartAttemptRepository _chartAttempts;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _user;

    public GetXXBestChartAttemptHandler(ICurrentUserAccessor user,
        IXXChartAttemptRepository chartAttempts,
        IChartRepository charts)
    {
        _user = user;
        _chartAttempts = chartAttempts;
        _charts = charts;
    }

    public async Task<BestXXChartAttempt> Handle(GetXXBestChartAttemptQuery request,
        CancellationToken cancellationToken)
    {
        var chart = await _charts.GetChart(MixEnum.XX, request.ChartId, cancellationToken);
        var bestAttempt = await _chartAttempts.GetBestAttempt(_user.User.Id, chart, cancellationToken);
        return new BestXXChartAttempt(chart, bestAttempt);
    }
}