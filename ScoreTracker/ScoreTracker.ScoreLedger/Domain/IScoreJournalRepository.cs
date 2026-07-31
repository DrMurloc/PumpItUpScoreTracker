using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Domain;

/// <summary>
///     Ledger-internal port for the score event journal (ADR-001 Q8). One row is one play,
///     keyed by (UserId, MixId, ChartId, OccurredAt); rows are never updated except to raise
///     IsBest, and never deleted. Reads power the Sessions page.
/// </summary>
internal interface IScoreJournalRepository
{
    /// <summary>
    ///     Records a play that became the record. Idempotent on the play key: the same play
    ///     already journaled as an observation is raised to IsBest rather than duplicated.
    /// </summary>
    Task Append(ScoreJournalEntry entry, CancellationToken cancellationToken);

    /// <summary>
    ///     Records plays that did NOT become the record — the official site's recently-played
    ///     list. Existing rows on the same play key are left exactly as they are, so an
    ///     observation can never demote a best, and re-importing the same window is free.
    /// </summary>
    Task AppendObservations(IReadOnlyList<ScoreJournalEntry> entries, CancellationToken cancellationToken);

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

    /// <summary>
    ///     Deletes a player's journal, optionally scoped to one mix. The append-only rule above
    ///     governs the *write* path — the importer must never rewrite history — and does not
    ///     bind the person whose history it is (docs/design/delete-my-data.md D8).
    /// </summary>
    Task DeleteForUser(Guid userId, MixEnum? mix, CancellationToken cancellationToken);
}

internal sealed record JournalSessionRows(
    Guid? SessionId,
    DateOnly? Day,
    MixEnum Mix,
    IReadOnlyList<ScoreJournalEntry> Rows);
