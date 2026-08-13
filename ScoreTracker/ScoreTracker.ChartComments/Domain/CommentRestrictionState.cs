namespace ScoreTracker.ChartComments.Domain;

/// <summary>Everything a <see cref="CommentRestriction" /> is made of, in one parameter.</summary>
internal sealed record CommentRestrictionState(
    Guid Id,
    Guid UserId,
    Guid CommunityId,
    Guid RestrictedByUserId,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LiftedAt = null);
