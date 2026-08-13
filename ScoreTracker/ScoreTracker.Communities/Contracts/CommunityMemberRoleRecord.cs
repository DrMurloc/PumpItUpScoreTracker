using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Communities.Contracts;

/// <summary>
///     One member's role in a community, without display identity — the lean shape comment
///     moderation joins against to answer "may this actor act on that author". Retained bans are
///     included, carrying <see cref="CommunityRole.Banned" />.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record CommunityMemberRoleRecord(Guid UserId, CommunityRole Role);
