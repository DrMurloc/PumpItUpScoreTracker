using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Distinct charts each member has journaled submissions on in a mix (full journal span) —
///     the Rankings "charts played" column. Same guard as the boards.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityPlayCountsQuery(Name CommunityName, MixEnum Mix)
    : IQuery<IReadOnlyDictionary<Guid, int>>;
