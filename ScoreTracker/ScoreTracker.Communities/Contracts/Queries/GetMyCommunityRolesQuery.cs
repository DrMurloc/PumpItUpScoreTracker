namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>The current user's role/permissions across every community they hold a membership row in.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyCommunityRolesQuery : IQuery<IEnumerable<MyCommunityRoleRecord>>;
