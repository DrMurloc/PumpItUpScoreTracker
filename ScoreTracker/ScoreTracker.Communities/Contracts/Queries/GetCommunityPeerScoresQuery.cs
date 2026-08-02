using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Scores your clubmates have on a set of charts, for the Sessions page's Community Peers
///     section. **User-created communities only** — World and the country communities are
///     joined automatically, so counting them would make "your peers" mean everybody
///     (docs/design/session-breakdown.md D7).
///     <para>
///         Charts nobody has played come back absent rather than empty. The caller sorts by
///         competitive closeness and never filters on it: a clubmate three levels away is still
///         a clubmate (D8).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityPeerScoresQuery(Guid UserId, MixEnum Mix, IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<CommunityPeerScore>>>;
