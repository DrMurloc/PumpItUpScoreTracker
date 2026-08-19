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
        // Only plays that could have seated a record count toward the chart numbers, on both
        // sides — the same rule the replay applies (SessionUndoReplay.BestOf). A chart this
        // session touched only with stage breaks loses no record when they go, and a chart whose
        // only survivors are stage breaks has no earlier best to fall back to, so it reads as
        // removed rather than restored. Every row still counts as a play removed: they do go.
        var chartIds = removed.Where(e => BestAttemptPolicy.CanBeRecord(e.IsStageBroken))
            .Select(e => e.ChartId).Distinct().ToArray();
        var survivors = (await journal.GetChartHistories(request.UserId, chartIds, cancellationToken))
            .Where(e => e.SessionId != request.SessionId && BestAttemptPolicy.CanBeRecord(e.IsStageBroken))
            .ToArray();

        // The count that matters is the second one: a chart with no earlier play cannot be put
        // back, only removed.
        var restored = chartIds.Count(id => survivors.Any(e => e.ChartId == id));
        return new ScoreSessionUndoPreview(session, restored, chartIds.Length - restored, removed.Count);
    }
}
