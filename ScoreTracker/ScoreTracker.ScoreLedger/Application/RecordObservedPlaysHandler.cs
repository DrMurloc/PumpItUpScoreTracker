using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Commands;
using ScoreTracker.ScoreLedger.Domain;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     Journals plays that never became a record, so a chart's history holds the attempts and
///     not only the improvements. Writes nothing to the ledger's record — that is
///     <see cref="UpdatePhoenixRecordHandler" />'s job, and a play arriving through both paths
///     collapses onto one row by its play key.
/// </summary>
internal sealed class RecordObservedPlaysHandler(IScoreJournalRepository journal)
    : IRequestHandler<RecordObservedPlaysCommand>
{
    public Task Handle(RecordObservedPlaysCommand request, CancellationToken cancellationToken)
    {
        var entries = request.Plays
            // A walk-off is never stored, whether or not it would have been a best.
            .Where(p => !BestAttemptPolicy.IsWalkOff(p.IsBroken, p.Score, p.Judgements))
            .Select(p => new ScoreJournalEntry(p.PlayedAt, request.Source, request.UserId, p.ChartId,
                p.Score, BestAttemptPolicy.PlateFor(p.IsBroken, p.Plate), p.IsBroken, request.Mix,
                request.SessionId, p.Judgements, false))
            .ToArray();

        return journal.AppendObservations(entries, cancellationToken);
    }
}
