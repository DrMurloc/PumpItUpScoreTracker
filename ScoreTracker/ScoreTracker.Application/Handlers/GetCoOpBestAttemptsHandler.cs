using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    GetCoOpBestAttemptsHandler : IRequestHandler<GetCoOpBestAttemptsQuery, IEnumerable<BestChartAttempt>>
{
    private readonly IChartAttemptRepository _chartAttemptRepository;
    private readonly IChartRepository _chartRepository;
    private readonly ICurrentUserAccessor _currentUser;

    public GetCoOpBestAttemptsHandler(IChartAttemptRepository chartAttemptRepository,
        IChartRepository chartRepository, ICurrentUserAccessor currentUser)
    {
        _chartAttemptRepository = chartAttemptRepository;
        _chartRepository = chartRepository;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<BestChartAttempt>> Handle(GetCoOpBestAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        var result = new List<BestChartAttempt>();
        var charts = await _chartRepository.GetCoOpCharts(cancellationToken);
        foreach (var chart in charts)
        {
            var bestAttempt =
                await _chartAttemptRepository.GetBestAttempt(_currentUser.UserId, chart, cancellationToken);
            result.Add(new BestChartAttempt(chart, bestAttempt));
        }

        return result;
    }
}