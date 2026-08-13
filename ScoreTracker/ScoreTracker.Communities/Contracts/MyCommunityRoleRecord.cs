using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     The current user's standing in one of their communities — the directory's role chips,
///     Manage gating, and comment moderation. Carries the id as well as the name: a comment is
///     keyed to a community by <see cref="CommunityId" />, so a consumer joining standing onto
///     comments must never go through the rename-able name.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record MyCommunityRoleRecord(Guid CommunityId, Name CommunityName, CommunityRole Role,
    CommunityPermission Permissions);
