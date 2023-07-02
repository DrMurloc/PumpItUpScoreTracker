using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    GetCoOpBestAttemptsHandler : IRequestHandler<GetXXCoOpBestAttemptsQuery, IEnumerable<BestXXChartAttempt>>
{
    private readonly IXXChartAttemptRepository _chartAttemptRepository;
    private readonly IChartRepository _chartRepository;
    private readonly ICurrentUserAccessor _currentUser;

    public GetCoOpBestAttemptsHandler(IXXChartAttemptRepository chartAttemptRepository,
        IChartRepository chartRepository, ICurrentUserAccessor currentUser)
    {
        _chartAttemptRepository = chartAttemptRepository;
        _chartRepository = chartRepository;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<BestXXChartAttempt>> Handle(GetXXCoOpBestAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        var charts = await _chartRepository.GetCoOpCharts(MixEnum.XX, cancellationToken);

        return await _chartAttemptRepository.GetBestAttempts(_currentUser.User.Id, charts, cancellationToken);
    }
}