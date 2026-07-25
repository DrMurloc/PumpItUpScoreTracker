using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     One community member's summary. Guarded like the community's boards: a private
///     community requires the caller to be a member, and the target must be a member —
///     shared membership is what makes their scores visible.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityPlayerProfileQuery(Name CommunityName, Guid UserId, MixEnum Mix)
    : IQuery<CommunityPlayerProfileRecord?>;
