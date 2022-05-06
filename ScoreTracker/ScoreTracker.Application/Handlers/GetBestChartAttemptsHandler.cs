using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    GetBestChartAttemptsHandler : IRequestHandler<GetBestChartAttemptsQuery,
        IEnumerable<BestChartAttempt>>
{
    private readonly IChartAttemptRepository _chartAttemptRepository;
    private readonly ICurrentUserAccessor _currentUser;

    public GetBestChartAttemptsHandler(IChartAttemptRepository chartAttemptRepository,
        IChartRepository chartRepository, ICurrentUserAccessor currentUser)
    {
        _chartAttemptRepository = chartAttemptRepository;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<BestChartAttempt>> Handle(GetBestChartAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        return await _chartAttemptRepository.GetBestAttempts(_currentUser.User.Id, cancellationToken);
    }
}