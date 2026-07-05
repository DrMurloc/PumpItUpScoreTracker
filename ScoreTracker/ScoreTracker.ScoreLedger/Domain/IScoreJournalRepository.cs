using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Ledger-internal port for the score event journal (ADR-001 Q8). Writes happen only
///     when the best attempt changed (progress-only); rows are never updated or deleted.
///     Reads power the Sessions page.
/// </summary>
internal interface IScoreJournalRepository
{
    Task Append(ScoreJournalEntry entry, CancellationToken cancellationToken);

    /// <summary>
    ///     Paged groups, newest activity first: one group per SessionId, and one per
    ///     calendar day for rows predating session capture. Rows ride along.
    /// </summary>
    Task<(int TotalGroups, IReadOnlyList<JournalSessionRows> Groups)> GetSessionGroups(MixEnum mix, Guid userId,
        int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Full journal history for the given charts, oldest first — classification input.</summary>
    Task<IReadOnlyList<ScoreJournalEntry>> GetChartHistories(MixEnum mix, Guid userId, IEnumerable<Guid> chartIds,
        CancellationToken cancellationToken);
}

internal sealed record JournalSessionRows(
    Guid? SessionId,
    DateOnly? Day,
    IReadOnlyList<ScoreJournalEntry> Rows);
