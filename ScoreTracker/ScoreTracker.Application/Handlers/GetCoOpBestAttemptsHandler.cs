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
        var charts = await _chartRepository.GetCoOpCharts(cancellationToken);

        return await _chartAttemptRepository.GetBestAttempts(_currentUser.UserId, charts, cancellationToken);
    }
}