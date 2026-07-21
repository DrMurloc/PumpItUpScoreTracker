namespace ScoreTracker.Communities.Contracts.Queries;

/// <summary>The invite landing page's preview; null when the code doesn't resolve to a community.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityInvitePreviewQuery(Guid InviteCode) : IQuery<CommunityInvitePreviewRecord?>;
