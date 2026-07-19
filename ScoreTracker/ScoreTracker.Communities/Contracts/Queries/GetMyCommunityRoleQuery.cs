using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     The current user's role + permissions in a community; a null role means they are not a
///     member. The Members tab uses this to decide which management controls to render — the
///     aggregate still enforces authorization server-side.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyCommunityRoleQuery(Name CommunityName) : IQuery<CommunityRoleRecord>;
