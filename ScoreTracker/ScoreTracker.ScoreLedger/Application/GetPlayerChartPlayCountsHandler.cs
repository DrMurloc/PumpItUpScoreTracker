using MediatR;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class GetPlayerChartPlayCountsHandler
    : IRequestHandler<GetPlayerChartPlayCountsQuery, IReadOnlyDictionary<Guid, int>>
{
    private readonly IScoreJournalRepository _journal;

    public GetPlayerChartPlayCountsHandler(IScoreJournalRepository journal)
    {
        _journal = journal;
    }

    public Task<IReadOnlyDictionary<Guid, int>> Handle(GetPlayerChartPlayCountsQuery request,
        CancellationToken cancellationToken)
    {
        return _journal.GetChartPlayCounts(request.UserId, request.Mix, cancellationToken);
    }
}
