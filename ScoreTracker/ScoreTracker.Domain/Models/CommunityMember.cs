using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Models;

/// <summary>
///     One member's standing in a community: their role, the permissions they hold (only
///     meaningful for admins), who granted their admin standing, and when they joined. The
///     <see cref="Community" /> aggregate is the source of truth; this is its projection.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityMember(
    Guid UserId,
    CommunityRole Role,
    CommunityPermission Permissions,
    Guid? GrantedBy,
    DateTimeOffset? JoinedAt);
