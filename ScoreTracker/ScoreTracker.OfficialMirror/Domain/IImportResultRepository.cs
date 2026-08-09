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
    ///     The runs behind a set of score sessions. The startup recovery pass arrives holding
    ///     session ids — it reads its candidates from the Ledger, which owns the "did the derived
    ///     work run" marker — and needs each one's run to decide whether the batch had its chance
    ///     to drain (docs/design/import-restart-recovery.md §3.1). A session with no run is a
    ///     manual, CSV or API submission and simply comes back absent.
    /// </summary>
    Task<IReadOnlyList<ImportRunForSession>> GetForSessions(IReadOnlyCollection<Guid> sessionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Stamps a run the process abandoned. Only ever closes an OPEN row, like
    ///     <see cref="Close" /> — a run that reported its own ending keeps that ending.
    /// </summary>
    Task MarkInterrupted(Guid id, DateTimeOffset finishedAt, CancellationToken cancellationToken = default);

    /// <summary>
    ///     The newest interrupted run this player has not yet been told about, or null. Drives the
    ///     one-time notice.
    /// </summary>
    Task<ImportAttemptRecord?> GetUnacknowledgedInterrupted(Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Records that the player has seen the notice for this run.</summary>
    Task Acknowledge(Guid id, DateTimeOffset at, CancellationToken cancellationToken = default);
}

/// <summary>An import run seen from its session — what the recovery pass needs to judge it.</summary>
internal sealed record ImportRunForSession(Guid Id, Guid SessionId, DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);
