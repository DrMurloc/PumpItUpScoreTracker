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
    ///     One page of a player's journal in one mix, newest first, for the partner API.
    ///     <para>
    ///         Keyset rather than offset: the journal is appended to while a caller is walking it, so
    ///         an offset would skip or repeat rows at every page boundary. The key is
    ///         (OccurredAt, ChartId) descending — <paramref name="beforeOccurredAt" /> and
    ///         <paramref name="beforeChartId" /> are the last row the caller saw, and rows are
    ///         append-only so a key never moves.
    ///     </para>
    /// </summary>
    Task<IReadOnlyList<ScoreJournalEntry>> GetJournalPage(Guid userId, MixEnum mix,
        DateTimeOffset? beforeOccurredAt, Guid? beforeChartId, DateTimeOffset? since, int limit,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Deletes a player's journal, optionally scoped to one mix. The append-only rule above
    ///     governs the *write* path — the importer must never rewrite history — and does not
    ///     bind the person whose history it is (docs/design/delete-my-data.md D8).
    /// </summary>
    Task DeleteForUser(Guid userId, MixEnum? mix, CancellationToken cancellationToken);

    /// <summary>Every play one session wrote, for the undo preview and the replay that follows.</summary>
    Task<IReadOnlyList<ScoreJournalEntry>> GetSessionEntries(Guid userId, Guid sessionId,
        CancellationToken cancellationToken);

    /// <summary>Removes one session's plays. The survivors are what the replay rebuilds from.</summary>
    Task DeleteSession(Guid userId, Guid sessionId, CancellationToken cancellationToken);
}

internal sealed record JournalSessionRows(
    Guid? SessionId,
    DateOnly? Day,
    MixEnum Mix,
    IReadOnlyList<ScoreJournalEntry> Rows);
