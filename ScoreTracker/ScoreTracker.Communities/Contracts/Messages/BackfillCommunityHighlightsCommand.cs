namespace ScoreTracker.Communities.Contracts.Messages;

/// <summary>
///     Admin bus trigger (docs/design/home-page-widgets.md §7): rebuild the community big-wins feed from
///     the last <see cref="Days" /> of captured highlights. Fire-and-forget so the admin request never
///     blocks on the reconstruction (and never rides — then loses — the Blazor circuit scope). Idempotent
///     (EventId = SessionId). PGs are not backfilled (not in the flagged-only highlight table).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record BackfillCommunityHighlightsCommand(int Days);
