using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     A community mute: you stay in the club and lose the mic. Community scope only — the site
///     scope already exists as <c>User.IsContentLocked</c>, and a second sitewide switch beside it
///     would be two switches for one decision. Prospective: existing comments stay unless
///     separately removed. A mute blocks post, reply and edit in its community — an edit is a way
///     to keep talking through old comments — while delete always works and votes are untouched,
///     because a vote is not content.
/// </summary>
internal sealed class CommentRestriction
{
    /// <summary>What the reason column can hold; anything longer is the moderator's essay, not a reason.</summary>
    public const int MaxReasonLength = 500;

    private CommentRestriction(CommentRestrictionState state)
    {
        Id = state.Id;
        UserId = state.UserId;
        CommunityId = state.CommunityId;
        RestrictedByUserId = state.RestrictedByUserId;
        Reason = state.Reason;
        CreatedAt = state.CreatedAt;
        LiftedAt = state.LiftedAt;
    }

    public Guid Id { get; }

    /// <summary>Whose mic this takes — the purge key. The moderator is a different person.</summary>
    public Guid UserId { get; }

    public Guid CommunityId { get; }
    public Guid RestrictedByUserId { get; }
    public string? Reason { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? LiftedAt { get; private set; }

    public bool IsActive => LiftedAt == null;

    public static CommentRestriction Impose(Guid userId, Guid communityId, Guid restrictedByUserId,
        string? reason, DateTimeOffset now)
    {
        if (userId == Guid.Empty || restrictedByUserId == Guid.Empty)
            throw new CommentNotAllowedException("A mute needs a target and a moderator.");
        if (userId == restrictedByUserId)
            throw new CommentNotAllowedException("You cannot mute yourself.");

        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed?.Length > MaxReasonLength)
            throw new CommentNotAllowedException(
                $"Keep the reason under {MaxReasonLength} characters.");

        return new CommentRestriction(new CommentRestrictionState(Guid.NewGuid(), userId, communityId,
            restrictedByUserId, trimmed, now));
    }

    /// <summary>Rehydration from storage — trusts what it is given.</summary>
    public static CommentRestriction FromStorage(CommentRestrictionState state)
    {
        return new CommentRestriction(state);
    }

    /// <summary>Idempotent: lifting a lifted mute keeps the first timestamp.</summary>
    public void Lift(DateTimeOffset now)
    {
        LiftedAt ??= now;
    }
}
