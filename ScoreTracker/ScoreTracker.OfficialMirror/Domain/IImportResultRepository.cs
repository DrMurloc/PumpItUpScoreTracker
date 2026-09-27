using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     One row per press of Import, Import and check, or Deep scan. Separate from the Ledger's
///     ScoreSession, which records what got SAVED: a session can span eight hours of play and
///     several runs, while an import is one attempt with one ending, and a failed one may have
///     no session worth showing at all.
/// </summary>
internal interface IImportResultRepository
{
    /// <summary>
    ///     Records that a run began, before any piugame call. Its existence is the only durable
    ///     proof an import was attempted — everything downstream can fail without leaving one.
    /// </summary>
    Task<Guid> Open(Guid userId, MixEnum mix, ImportKind kind, string? cardId, DateTimeOffset startedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stamps how it ended. A row this is never called for is a run the process never closed,
    ///     which is a real state and not a bug in the caller.
    /// </summary>
    Task Close(Guid id, DateTimeOffset finishedAt, ImportOutcome outcome, int? scoreCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Points the run at the score session it saved into, once one exists. Separate from Open
    ///     because a run that dies before its first save legitimately has none.
    /// </summary>
    Task AttachSession(Guid id, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     A player's most recent runs, newest first. ScoreCount comes back null here — the count
    ///     lives on the Ledger's session, and a vertical never joins onto another's tables; the
    ///     handler fills it in through a published contract.
    /// </summary>
    Task<IReadOnlyList<ImportAttemptRecord>> GetRecent(Guid userId, int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Closes every run that never reported back and began before <paramref name="startedBefore" />,
    ///     as Interrupted and already acknowledged: a notice about a press that old helps nobody, and
    ///     its session is too old to announce. Only OPEN rows, like <see cref="Close" />. Returns how
    ///     many it closed.
    /// </summary>
    Task<int> CloseAbandoned(DateTimeOffset startedBefore, DateTimeOffset at,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Every run that began at or after <paramref name="from" /> and before <paramref name="before" />
    ///     and never reported an ending, newest first — the startup pass's candidates
    ///     (docs/design/import-restart-recovery.md §0).
    /// </summary>
    Task<IReadOnlyList<ImportRunForRecovery>> GetUnfinishedStartedBetween(DateTimeOffset from,
        DateTimeOffset before, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Runs that began at or after <paramref name="since" />, saved into a session, and ended in
    ///     anything but Completed — the one way an import's announcement can go missing on a live
    ///     process. Filtered on when the run began, not only when it ended: <see cref="CloseAbandoned" />
    ///     stamps an ending on runs far older than that, and those are closed without a card.
    /// </summary>
    Task<IReadOnlyList<ImportRunForRecovery>> GetFailedSince(DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stamps a run the process abandoned. Only ever closes an OPEN row, like
    ///     <see cref="Close" /> — a run that reported its own ending keeps that ending.
    /// </summary>
    Task MarkInterrupted(Guid id, DateTimeOffset finishedAt, CancellationToken cancellationToken = default);

    /// <summary>
    ///     The interrupted run this player has not yet been told about, or null. Drives the
    ///     one-time notice.
    ///     <para>
    ///         Only ever their **most recent** run qualifies. The notice says "import again", so a
    ///         later run of any kind makes it stale advice — and since a run is only marked
    ///         Interrupted at the next boot, a player who imports again before that boot would
    ///         otherwise be told to do the thing they just did.
    ///     </para>
    /// </summary>
    Task<ImportAttemptRecord?> GetUnacknowledgedInterrupted(Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Records that the player has seen the notice for this run.</summary>
    Task Acknowledge(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default);
}

/// <summary>An import run as the recovery passes see it: whose, which session, and whether it ended.</summary>
internal sealed record ImportRunForRecovery(Guid Id, Guid UserId, Guid? SessionId, DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);
