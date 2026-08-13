namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>
///     Every membership row of one community as (user, role) pairs, bans included. Keyed by id
///     rather than name so consumers holding a stored community reference never resolve through
///     the name. An unknown id returns empty.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityMemberRolesQuery(Guid CommunityId)
    : IQuery<IEnumerable<CommunityMemberRoleRecord>>;
