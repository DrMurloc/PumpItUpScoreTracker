using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Contracts;

/// <summary>The current user's standing in a community — drives which management controls the UI shows.</summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityRoleRecord(CommunityRole? Role, CommunityPermission Permissions);
