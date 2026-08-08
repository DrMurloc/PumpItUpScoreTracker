namespace ScoreTracker.ChartComments.Contracts;

/// <summary>
///     Who a comment is for. The scope rail on the Comments tab is this type — it filters what you
///     read <em>and</em> decides what you post to, so "which community am I posting to" is answered
///     by where the reader is standing rather than by a second control.
/// </summary>
public enum CommentAudienceKind
{
    /// <summary>Everyone, signed in or not. Moderated by the site admin.</summary>
    Public,

    /// <summary>
    ///     An audience of one. Never translated, never moderated, never voted on, never replied to
    ///     — a personal note is the same row as a comment and nothing else about it is the same.
    /// </summary>
    Private,

    /// <summary>
    ///     One non-regional community, moderated by its own admins. World and country boards are
    ///     ownerless and carry no roles, so a comment there would have no moderator.
    /// </summary>
    Community
}

/// <summary>
///     An audience and, when it is a community, which one. A reply inherits its root's value as a
///     domain invariant rather than as a field the UI sets, so a thread cannot be moved between
///     communities or between a community and the public.
/// </summary>
[ExcludeFromCodeCoverage]
public readonly record struct CommentAudience
{
    private CommentAudience(CommentAudienceKind kind, Guid? communityId)
    {
        Kind = kind;
        CommunityId = communityId;
    }

    public CommentAudienceKind Kind { get; }

    /// <summary>Set only when <see cref="Kind" /> is <see cref="CommentAudienceKind.Community" />.</summary>
    public Guid? CommunityId { get; }

    public static CommentAudience Public { get; } = new(CommentAudienceKind.Public, null);

    public static CommentAudience Private { get; } = new(CommentAudienceKind.Private, null);

    public static CommentAudience Community(Guid communityId)
    {
        // Keyed by id rather than by name: the Communities contracts are name-keyed, and a club
        // that renames would otherwise strand every thread it ever held.
        if (communityId == Guid.Empty)
            throw new ArgumentException("A community audience needs a community.", nameof(communityId));

        return new CommentAudience(CommentAudienceKind.Community, communityId);
    }

    public bool IsPrivate => Kind == CommentAudienceKind.Private;

    public bool IsPublic => Kind == CommentAudienceKind.Public;

    public override string ToString()
    {
        return Kind == CommentAudienceKind.Community ? $"{Kind}:{CommunityId}" : Kind.ToString();
    }
}
