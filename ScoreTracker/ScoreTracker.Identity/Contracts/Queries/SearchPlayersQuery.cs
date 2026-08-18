namespace ScoreTracker.Identity.Contracts.Queries;

/// <summary>
///     The per-keystroke player search: site name or game tag containing the term, over the
///     players the caller may look at — public players plus the visibility port's audience —
///     best matches first, capped. Anonymous callers get public players only. Empty for a blank
///     term (docs/design/player-page-and-site-search.md D13–D15).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SearchPlayersQuery(string Term, int Take = 10) : IQuery<IReadOnlyList<PlayerSearchHit>>;
