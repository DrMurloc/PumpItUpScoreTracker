using MediatR;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.Domain.Models.Titles.XX;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Application.Handlers;

public sealed class GetTitleProgressHandler : IRequestHandler<GetTitleProgressQuery, IEnumerable<TitleProgress>>
{
    private readonly IXXChartAttemptRepository _chartAttempts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IPhoenixRecordRepository _phoenixScores;

    public GetTitleProgressHandler(ICurrentUserAccessor currentUser,
        IXXChartAttemptRepository chartAttempts,
        IPhoenixRecordRepository phoenixScores)
    {
        _currentUser = currentUser;
        _chartAttempts = chartAttempts;
        _phoenixScores = phoenixScores;
    }

    public async Task<IEnumerable<TitleProgress>> Handle(GetTitleProgressQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Mix == MixEnum.XX)
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

            return XXTitleList.BuildProgress(attempts);
        }

        IEnumerable<RecordedPhoenixScore> scores;
        if (_currentUser.IsLoggedIn)
        {
            var userId = _currentUser.User.Id;
            scores = await _phoenixScores.GetRecordedScores(userId, cancellationToken);
        }
        else
        {
            scores = Array.Empty<RecordedPhoenixScore>();
        }

        return PhoenixTitleList.BuildProgress(scores);
    }
}