using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    GetBestChartAttemptsHandler : IRequestHandler<GetBestChartAttemptsQuery,
        IEnumerable<BestChartAttempt>>
{
    private readonly IChartAttemptRepository _chartAttemptRepository;
    private readonly IUserAccessService _userAccess;

    public GetBestChartAttemptsHandler(IChartAttemptRepository chartAttemptRepository,
        IUserAccessService userAccess)
    {
        _chartAttemptRepository = chartAttemptRepository;
        _userAccess = userAccess;
    }

    public async Task<IEnumerable<BestChartAttempt>> Handle(GetBestChartAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await _userAccess.HasAccessTo(request.UserId, cancellationToken)) return Array.Empty<BestChartAttempt>();

        return await _chartAttemptRepository.GetBestAttempts(request.UserId, cancellationToken);
    }
}