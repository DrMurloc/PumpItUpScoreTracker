using ScoreTracker.PlayerProgress.Contracts.Events;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     Reconstructs recent capture events from persisted highlights + title milestones since a cutoff —
///     the source for the community big-wins backfill (docs/design/rivals.md §2.4). Reconstructed
///     changes carry no plate: PGs live in the score journal, not the highlight table, so the backfill
///     covers the flag-based wins + titles and PGs accrue live. EventId = SessionId, so re-runs are stable.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetRecentHighlightEventsQuery(DateTimeOffset Since)
    : IQuery<IEnumerable<ScoreHighlightsCapturedEvent>>;
