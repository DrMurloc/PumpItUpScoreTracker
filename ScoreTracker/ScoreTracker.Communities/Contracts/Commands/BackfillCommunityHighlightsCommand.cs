using MediatR;

namespace ScoreTracker.Communities.Contracts.Commands;

/// <summary>
///     Admin: rebuild the community big-wins feed from the last <paramref name="Days" /> of captured
///     highlights (docs/design/home-page-widgets.md §7). Reconstructs events from persisted highlights +
///     title milestones and runs them through the same capture path — idempotent (EventId = SessionId).
///     PGs are not backfilled (they live in the score journal, not the highlight table); they accrue live.
///     Returns the number of reconstructed events processed.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BackfillCommunityHighlightsCommand(int Days) : IRequest<int>;
