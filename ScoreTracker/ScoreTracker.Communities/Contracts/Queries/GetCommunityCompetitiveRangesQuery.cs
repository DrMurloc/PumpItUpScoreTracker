using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Per-community competitive-level spreads for the directory: Singles and Doubles min–max
///     over members with stats on the mix and a competitive level of at least 5. Communities
///     with no qualifying members are absent. Heavily cached — staleness up to a day is fine.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityCompetitiveRangesQuery(MixEnum Mix)
    : IQuery<IEnumerable<CommunityCompetitiveRangeRecord>>;
