namespace ScoreTracker.ScoreLedger.Contracts.Queries;

/// <summary>
///     The session holding one stored session, from anywhere in the player's history — what a
///     <c>?session=</c> deep link opens. Every import still sends its own Discord card and every card
///     links its own import, so each of a night's cards has to open the same folded session
///     (docs/design/session-breakdown.md D55).
///     <para>
///         Null when no play carries that id any more, which only an undo does, and for a non-public
///         player read by anyone but themselves — the same defense in depth as
///         <see cref="GetRecentSessionsQuery" />.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSessionContainingQuery(Guid UserId, Guid SessionId)
    : IQuery<RecentSessionsPage.SessionGroup?>;
