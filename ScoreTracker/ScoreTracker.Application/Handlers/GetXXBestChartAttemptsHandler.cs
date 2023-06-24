using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;

namespace ScoreTracker.Application.Handlers;

public sealed class
    GetXXBestChartAttemptsHandler : IRequestHandler<GetXXBestChartAttemptsQuery,
        IEnumerable<BestXXChartAttempt>>
{
    private readonly IXXChartAttemptRepository _chartAttemptRepository;
    private readonly IUserAccessService _userAccess;

    public GetXXBestChartAttemptsHandler(IXXChartAttemptRepository chartAttemptRepository,
        IUserAccessService userAccess)
    {
        _chartAttemptRepository = chartAttemptRepository;
        _userAccess = userAccess;
    }

    public async Task<IEnumerable<BestXXChartAttempt>> Handle(GetXXBestChartAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        if (!await _userAccess.HasAccessTo(request.UserId, cancellationToken)) return Array.Empty<BestXXChartAttempt>();

        return await _chartAttemptRepository.GetBestAttempts(request.UserId, cancellationToken);
    }
}