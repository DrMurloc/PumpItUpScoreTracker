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
    Task Close(Guid id, DateTimeOffset finishedAt, ImportOutcome outcome,
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
}
