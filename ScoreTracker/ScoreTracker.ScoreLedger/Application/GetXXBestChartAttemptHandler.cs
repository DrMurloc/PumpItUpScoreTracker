using MediatR;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class GetXXBestChartAttemptHandler : IRequestHandler<GetXXBestChartAttemptQuery, BestXXChartAttempt>
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
        var chart = await _charts.GetChart(request.Mix, request.ChartId, cancellationToken);
        var bestAttempt = await _chartAttempts.GetBestAttempt(_user.User.Id, chart, cancellationToken);
        return new BestXXChartAttempt(chart, bestAttempt);
    }
}