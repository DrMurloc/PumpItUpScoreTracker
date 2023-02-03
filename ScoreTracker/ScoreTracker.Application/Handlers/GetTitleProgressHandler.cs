using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetTitleProgressHandler : IRequestHandler<GetTitleProgressQuery, IEnumerable<TitleProgress>>
{
    private readonly IChartAttemptRepository _chartAttempts;
    private readonly ICurrentUserAccessor _currentUser;

    public GetTitleProgressHandler(ICurrentUserAccessor currentUser,
        IChartAttemptRepository chartAttempts)
    {
        _currentUser = currentUser;
        _chartAttempts = chartAttempts;
    }

    public async Task<IEnumerable<TitleProgress>> Handle(GetTitleProgressQuery request,
        CancellationToken cancellationToken)
    {
        IEnumerable<BestChartAttempt> attempts;
        if (_currentUser.IsLoggedIn)
        {
            var userId = _currentUser.User.Id;
            attempts = await _chartAttempts.GetBestAttempts(userId, cancellationToken);
        }
        else
        {
            attempts = Array.Empty<BestChartAttempt>();
        }

        return TitleList.BuildProgress(attempts);
    }
}