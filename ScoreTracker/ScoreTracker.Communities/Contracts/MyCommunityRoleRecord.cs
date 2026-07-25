using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>The current user's standing in one of their communities — the directory's role chips and Manage gating.</summary>
[ExcludeFromCodeCoverage]
public sealed record MyCommunityRoleRecord(Name CommunityName, CommunityRole Role, CommunityPermission Permissions);
