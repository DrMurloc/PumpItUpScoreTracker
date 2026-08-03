using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class GetPlayerJournalHandler
    : IRequestHandler<GetPlayerJournalQuery, IReadOnlyList<ScoreJournalEntry>>
{
    private readonly IScoreJournalRepository _journal;

    public GetPlayerJournalHandler(IScoreJournalRepository journal)
    {
        _journal = journal;
    }

    public async Task<IReadOnlyList<ScoreJournalEntry>> Handle(GetPlayerJournalQuery request,
        CancellationToken cancellationToken)
    {
        return await _journal.GetJournalPage(request.UserId, request.Mix, request.BeforeOccurredAt,
            request.BeforeChartId, request.Since, Math.Clamp(request.Limit, 1, 500), cancellationToken);
    }
}
