using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain;

/// <summary>
///     The significant-win ledger, keyed by player rather than by audience
///     (docs/design/rivals.md §2.4). Vertical-internal — every reader outside PlayerProgress goes
///     through the published contract queries.
///     <para>
///         Communities keeps its own (EventId × CommunityId) index for the seek a World-scoped
///         feed needs; it reads the payloads back from here. Rivals has no index at all: a rival
///         list is small enough to fan in on read, which is also what lets adding a rival surface
///         their last 30 days immediately instead of only what happens next.
///     </para>
/// </summary>
internal interface IPlayerHighlightRepository
{
    /// <summary>
    ///     Persists one event's wins. Returns false when the event was already stored — the write
    ///     is idempotent on the event id, so a redelivery or a re-run backfill is a no-op.
    /// </summary>
    Task<bool> Add(Guid eventId, Guid userId, MixEnum mix, DateTimeOffset occurredAt, Guid? sessionId,
        IReadOnlyList<SignificantWin> wins, CancellationToken cancellationToken);

    /// <summary>Recent wins for a set of players in a mix, newest first. Stale schema rows are skipped.</summary>
    Task<IReadOnlyList<PlayerHighlightEntry>> GetForUsers(IReadOnlyCollection<Guid> userIds, MixEnum mix,
        int take, CancellationToken cancellationToken);

    /// <summary>
    ///     Payloads for specific events — how an audience index (Communities') turns its rows back
    ///     into wins. Order is the caller's to impose; it already knows the one it wants.
    /// </summary>
    Task<IReadOnlyList<PlayerHighlightEntry>> GetForEvents(IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken);

    /// <summary>Drop summaries older than the cutoff. Returns rows removed.</summary>
    Task<int> PurgeBefore(DateTimeOffset cutoff, CancellationToken cancellationToken);
}

/// <summary>A read row — the winner's id (name/avatar resolved at read) plus the win list.</summary>
internal sealed record PlayerHighlightEntry(
    Guid EventId,
    Guid UserId,
    MixEnum Mix,
    DateTimeOffset OccurredAt,
    Guid? SessionId,
    IReadOnlyList<SignificantWin> Wins);
