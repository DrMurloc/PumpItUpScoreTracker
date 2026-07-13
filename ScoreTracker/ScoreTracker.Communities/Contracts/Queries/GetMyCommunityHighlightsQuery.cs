using ScoreTracker.Communities.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Recent big wins across the communities the caller chose, newest first, deduped per event.
///     Membership-gated to the caller (CH2); <paramref name="IncludeOwnWins" /> keeps or drops the
///     caller's own rows (default on, CH4).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyCommunityHighlightsQuery(
    IReadOnlyCollection<Name> Communities,
    MixEnum Mix,
    bool IncludeOwnWins,
    int Take) : IQuery<IEnumerable<CommunityHighlightRecord>>;
