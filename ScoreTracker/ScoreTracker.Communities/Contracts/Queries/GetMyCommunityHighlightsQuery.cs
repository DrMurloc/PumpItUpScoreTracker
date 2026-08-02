using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Recent big wins across the communities the caller chose, newest first, deduped per event.
///     Membership-gated to the caller (CH2); <paramref name="IncludeOwnWins" /> keeps or drops the
///     caller's own rows (default on, CH4).
///     <para>
///         Returns PlayerProgress's record rather than a community-shaped one: the wins are the
///         same wins whichever audience surfaced them, and a second record type would only differ
///         by which vertical happened to answer (docs/design/rivals.md D32).
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyCommunityHighlightsQuery(
    IReadOnlyCollection<Name> Communities,
    MixEnum Mix,
    bool IncludeOwnWins,
    int Take) : IQuery<IEnumerable<PlayerHighlightRecord>>;
