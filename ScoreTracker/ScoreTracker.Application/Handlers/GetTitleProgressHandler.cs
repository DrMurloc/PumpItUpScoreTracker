using MediatR;
using ScoreTracker.Application.Queries;
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
        var userId = _currentUser.User.Id;
        var attempts = await _chartAttempts.GetBestAttempts(userId, cancellationToken);
        return TitleList.BuildProgress(attempts);
    }
}