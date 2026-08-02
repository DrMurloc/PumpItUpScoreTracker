using ScoreTracker.SharedKernel.Enums;

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
