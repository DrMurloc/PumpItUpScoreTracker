using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>The full member roster (including retained bans) with each member's role and permissions.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityRosterQuery(Name CommunityName) : IQuery<IEnumerable<CommunityMemberRecord>>;
