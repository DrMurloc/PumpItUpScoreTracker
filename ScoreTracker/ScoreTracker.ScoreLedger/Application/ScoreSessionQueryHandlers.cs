using MediatR;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

internal sealed class ScoreSessionQueryHandlers(IScoreSessionRepository sessions, IScoreJournalRepository journal,
        IPhoenixRecordRepository records)
    : IRequestHandler<GetScoreSessionsQuery, IReadOnlyList<ScoreSessionRecord>>,
        IRequestHandler<GetScoreSessionUndoPreviewQuery, ScoreSessionUndoPreview?>,
        IRequestHandler<GetMixesWithScoresQuery, IReadOnlyList<MixScoreCount>>
{
    public Task<IReadOnlyList<MixScoreCount>> Handle(GetMixesWithScoresQuery request,
        CancellationToken cancellationToken)
    {
        return records.GetMixesWithScores(request.UserId, cancellationToken);
    }

    public Task<IReadOnlyList<ScoreSessionRecord>> Handle(GetScoreSessionsQuery request,
        CancellationToken cancellationToken)
    {
        return sessions.ListFor(request.UserId, cancellationToken);
    }

    public async Task<ScoreSessionUndoPreview?> Handle(GetScoreSessionUndoPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var session = await sessions.Get(request.SessionId, cancellationToken);
        if (session == null || session.UserId != request.UserId) return null;

        var removed = await journal.GetSessionEntries(request.UserId, request.SessionId, cancellationToken);
        var chartIds = removed.Select(e => e.ChartId).Distinct().ToArray();
        var survivors = (await journal.GetChartHistories(request.UserId, chartIds, cancellationToken))
            .Where(e => e.SessionId != request.SessionId)
            .ToArray();

        // The count that matters is the second one: a chart with no earlier play cannot be put
        // back, only removed.
        var restored = chartIds.Count(id => survivors.Any(e => e.ChartId == id));
        return new ScoreSessionUndoPreview(session, restored, chartIds.Length - restored, removed.Count);
    }
}
