using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class GetChartScoreJourneyHandler
    : IRequestHandler<GetChartScoreJourneyQuery, IEnumerable<ScoreJournalEntry>>
{
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IScoreJournalRepository _journal;
    private readonly IUserAccessService _userAccess;

    public GetChartScoreJourneyHandler(IScoreJournalRepository journal, ICurrentUserAccessor currentUser,
        IUserAccessService userAccess)
    {
        _journal = journal;
        _currentUser = currentUser;
        _userAccess = userAccess;
    }

    public async Task<IEnumerable<ScoreJournalEntry>> Handle(GetChartScoreJourneyQuery request,
        CancellationToken cancellationToken)
    {
        var userId = request.UserId ?? _currentUser.User.Id;
        if (!await _userAccess.HasAccessTo(userId, cancellationToken))
            return Array.Empty<ScoreJournalEntry>();

        return await _journal.GetChartHistories(userId, new[] { request.ChartId }, cancellationToken);
    }
}
