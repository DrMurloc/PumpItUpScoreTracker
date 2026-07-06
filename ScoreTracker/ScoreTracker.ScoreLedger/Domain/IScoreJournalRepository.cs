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
    ///     Paged groups across every mix, newest activity first: one group per
    ///     SessionId, and one per (mix, calendar day) for rows predating session
    ///     capture. Rows ride along; each group carries its mix.
    /// </summary>
    Task<(int TotalGroups, IReadOnlyList<JournalSessionRows> Groups)> GetSessionGroups(Guid userId,
        int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    ///     Full journal history for the given charts, oldest first — classification
    ///     input. Chart ids are mix-scoped by construction, so no mix filter is needed.
    /// </summary>
    Task<IReadOnlyList<ScoreJournalEntry>> GetChartHistories(Guid userId, IEnumerable<Guid> chartIds,
        CancellationToken cancellationToken);
}

internal sealed record JournalSessionRows(
    Guid? SessionId,
    DateOnly? Day,
    MixEnum Mix,
    IReadOnlyList<ScoreJournalEntry> Rows);
