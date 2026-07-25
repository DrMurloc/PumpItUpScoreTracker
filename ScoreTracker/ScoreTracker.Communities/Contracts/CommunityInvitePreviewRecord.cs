using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     What the invite landing page shows before the user accepts: which community the code
///     opens, its shape, and the caller's standing (banned / already a member / expired code).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityInvitePreviewRecord(
    Name CommunityName,
    CommunityPrivacyType PrivacyType,
    int MemberCount,
    DateOnly? ExpirationDate,
    bool IsExpired,
    bool IsBanned,
    bool IsAlreadyMember);
