using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Rivals.Contracts.Queries;

/// <summary>
///     The caller's roster, resolved. Unbounded by design (D17) — a few hundred is accepted
///     overhead, handled at each surface rather than by a cap here.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyRivalsQuery(MixEnum Mix) : IQuery<IReadOnlyList<RivalSubject>>;

/// <summary>
///     Every arrow pointing at the caller. The counterweight to the whole system: since an edge
///     never lapses on its own, this list is the only revocation there is (D14), so it must never
///     omit a row for being inconvenient — a private player's arrow at a public one shows too.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetRivalsOfMeQuery : IQuery<IReadOnlyList<RivalOfMeRecord>>;

[ExcludeFromCodeCoverage]
public sealed record GetMyBlockedPlayersQuery : IQuery<IReadOnlyList<BlockedPlayerRecord>>;

/// <summary>
///     The caller's code, minted on first read. Null when the caller is PUBLIC: a public account
///     has nothing to hand out, and a code nobody needs reads as a step they missed (D23).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyRivalInviteCodeQuery : IQuery<string?>;

/// <summary>Who a code belongs to, for the landing page. Null when it matches nobody.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetRivalInvitePreviewQuery(string Code) : IQuery<RivalInvitePreviewRecord?>;

/// <summary>
///     Site-side picker: public players plus the caller's community members (D20). A private
///     stranger is not findable at all — which is what the invite code exists for.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SearchRivalCandidatesQuery(string Term, int Take = 20)
    : IQuery<IReadOnlyList<RivalCandidateRecord>>;

/// <summary>
///     Board-side picker. Narrowed to tags that placed in the latest sealed snapshot (D21):
///     offering a departed tag hands somebody a permanently empty rivalry.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SearchRivalTagsQuery(MixEnum Mix, string Term, int Take = 20)
    : IQuery<IReadOnlyList<string>>;

/// <summary>
///     Rival scores for a set of charts — the ONE read every "what did my rivals get on this"
///     surface goes through (docs/design/rivals.md §2.5), so the ghost/live seam is solved once.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetRivalScoresForChartsQuery(MixEnum Mix, IReadOnlyCollection<Guid> ChartIds)
    : IQuery<RivalChartScores>;

/// <summary>
///     One rival, compared. <paramref name="ChartType" />/<paramref name="Level" /> pick a folder
///     for a site rival; a board-only rival ignores them and returns the charts you are BOTH on,
///     which is the honest unit when the mirror covers level 20+ at roughly 300 deep (D29).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetRivalHeadToHeadQuery(MixEnum Mix, Guid EdgeId, ChartType? ChartType = null,
    DifficultyLevel? Level = null) : IQuery<RivalHeadToHeadRecord?>;

/// <summary>
///     The rivals feed. Ghosts never appear (D30) — wins come from imports, and a board-only
///     player has none.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyRivalHighlightsQuery(MixEnum Mix, int Take) : IQuery<IEnumerable<PlayerHighlightRecord>>;
