using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetTitleProgressHandler : IRequestHandler<GetXXTitleProgressQuery, IEnumerable<TitleProgress>>
{
    private readonly IXXChartAttemptRepository _chartAttempts;
    private readonly ICurrentUserAccessor _currentUser;

    public GetTitleProgressHandler(ICurrentUserAccessor currentUser,
        IXXChartAttemptRepository chartAttempts)
    {
        _currentUser = currentUser;
        _chartAttempts = chartAttempts;
    }

    public async Task<IEnumerable<TitleProgress>> Handle(GetXXTitleProgressQuery request,
        CancellationToken cancellationToken)
    {
        IEnumerable<BestXXChartAttempt> attempts;
        if (_currentUser.IsLoggedIn)
        {
            var userId = _currentUser.User.Id;
            attempts = await _chartAttempts.GetBestAttempts(userId, cancellationToken);
        }
        else
        {
            attempts = Array.Empty<BestXXChartAttempt>();
        }

        return TitleList.BuildProgress(attempts);
    }
}