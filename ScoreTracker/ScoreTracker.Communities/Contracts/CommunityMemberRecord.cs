using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>One roster row for the Members tab: the user's display identity plus their role/permissions.</summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityMemberRecord(
    Guid UserId,
    Name Name,
    Uri ProfileImage,
    CommunityRole Role,
    CommunityPermission Permissions);
