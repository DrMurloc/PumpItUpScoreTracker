using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     Recent significant wins for a set of players in a mix, newest first
///     (docs/design/rivals.md §2.4). Fan-in on read: the caller has already decided who its
///     audience is, which is what lets a rivals feed show somebody's last 30 days the moment
///     they are added rather than only what happens next.
///     <para>
///         This query applies NO consent rule of its own — the caller's audience is the rule.
///         Communities gates on membership before it gets here; Rivals gates at add time.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPlayerHighlightsQuery(
    IReadOnlyCollection<Guid> UserIds,
    MixEnum Mix,
    int Take) : IQuery<IEnumerable<PlayerHighlightRecord>>;
