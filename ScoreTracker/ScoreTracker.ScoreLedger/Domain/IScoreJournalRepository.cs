using ScoreTracker.Domain.Records;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Ledger-internal append port for the score event journal (ADR-001 Q8). Submissions
///     are journaled as received — including ones that don't beat the stored best —
///     because the journal is play history, not best-attempt state. Append-only.
/// </summary>
internal interface IScoreJournalRepository
{
    Task Append(ScoreJournalEntry entry, CancellationToken cancellationToken);
}
