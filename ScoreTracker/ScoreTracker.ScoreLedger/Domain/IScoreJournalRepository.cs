using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

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
        int page, int pageSize, DateTimeOffset? before, CancellationToken cancellationToken);

    /// <summary>
    ///     Full journal history for the given charts, oldest first — classification input.
    ///     <para>
    ///         ⚠ <b>CROSS-MIX, and callers must filter.</b> A returning song carries one ChartId
    ///         across Phoenix and Phoenix 2, so this returns both mixes' plays for such a chart.
    ///         That is exactly what reclear detection wants and exactly what anything rebuilding
    ///         one mix's record must drop first. This comment previously claimed chart ids were
    ///         mix-scoped; they are not, and the undo replay trusted it.
    ///     </para>
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

    /// <summary>
    ///     One row per public player ever observed CLEARING this chart, carrying their lowest such
    ///     score — the limbo board (docs/design/limbo-leaderboard.md). Ascending, capped.
    ///     <para>
    ///         Breaks are excluded outright: failing with a low score is not the achievement,
    ///         surviving with one is (D4). Private players are dropped rather than masked, which is
    ///         what the World scope shows too.
    ///     </para>
    /// </summary>
    Task<IReadOnlyList<UserPhoenixScore>> GetLowestPassingPlays(MixEnum mix, Guid chartId, int limit,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Journal rows per chart for one player in one mix. Charts with no row are absent, not
    ///     zero. Reads the (UserId, MixId, ChartId, OccurredAt) index end to end, so the whole
    ///     mix costs one grouped scan of that player's slice.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetChartPlayCounts(Guid userId, MixEnum mix,
        CancellationToken cancellationToken);

    /// <summary>Every play one session wrote, for the undo preview and the replay that follows.</summary>
    Task<IReadOnlyList<ScoreJournalEntry>> GetSessionEntries(Guid userId, Guid sessionId,
        CancellationToken cancellationToken);

    /// <summary>Removes one session's plays. The survivors are what the replay rebuilds from.</summary>
    Task DeleteSession(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    ///     A player's judgement-carrying rows in one mix, newest first, capped — the score
    ///     calculator's "load one of your plays" list. Stage breaks are excluded (a partial
    ///     screen is not a screen); finished fails stay in.
    /// </summary>
    Task<IReadOnlyList<ScoreJournalEntry>> GetJudgedPlays(Guid userId, MixEnum mix, int limit,
        CancellationToken cancellationToken);

    /// <summary>Every player holding at least one judged row in the mix — the backfill's work list.</summary>
    Task<IReadOnlyList<Guid>> GetUsersWithJudgedEntries(MixEnum mix, CancellationToken cancellationToken);

    /// <summary>One player's judged rows in one mix, for the backfill to re-solve.</summary>
    Task<IReadOnlyList<ScoreJournalEntry>> GetJudgedEntries(Guid userId, MixEnum mix,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Writes re-solved combos onto one player's rows in one mix, keyed by the play key's
    ///     chart and time. The one sanctioned in-place write besides raising IsBest: the combo is a
    ///     function of the row's other columns and the catalog, not history.
    /// </summary>
    Task SetMaxCombos(Guid userId, MixEnum mix,
        IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, int? MaxCombo)> combos,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Writes re-solved stage-break causes onto one player's rows in one mix, keyed by the
    ///     play key's chart and time. Derived like the combo beside it — a function of the row's
    ///     judgements, the catalog and the mix's grade floors — so it is re-derived wholesale
    ///     whenever any of those improve (docs/design/pass-command-detection.md).
    /// </summary>
    Task SetStageBreakCauses(Guid userId, MixEnum mix,
        IReadOnlyList<(Guid ChartId, DateTimeOffset OccurredAt, StageBreakCause Cause)> causes,
        CancellationToken cancellationToken);
}

internal sealed record JournalSessionRows(
    Guid? SessionId,
    DateOnly? Day,
    MixEnum Mix,
    IReadOnlyList<ScoreJournalEntry> Rows);
