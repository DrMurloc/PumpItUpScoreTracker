using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     One comment, reply or personal note on a chart. A rich aggregate in the
///     <c>TournamentSession</c> class rather than a property bag, because the rules here are dense
///     and every one of them is a way for somebody's words to end up in front of the wrong audience.
///     <para>
///         The clock arrives as a parameter, never from <c>DateTimeOffset.Now</c>: this type is pure
///         and its tests need no seam.
///     </para>
/// </summary>
internal sealed class Comment
{
    private Comment(Guid id, Guid chartId, Guid userId, CommentAudience audience, Guid? parentCommentId,
        string text, DateTimeOffset createdAt, DateTimeOffset? editedAt, DateTimeOffset? deletedAt,
        Guid? deletedByUserId, string? sourceLanguage)
    {
        Id = id;
        ChartId = chartId;
        UserId = userId;
        Audience = audience;
        ParentCommentId = parentCommentId;
        Text = text;
        CreatedAt = createdAt;
        EditedAt = editedAt;
        DeletedAt = deletedAt;
        DeletedByUserId = deletedByUserId;
        SourceLanguage = sourceLanguage;
    }

    public Guid Id { get; }
    public Guid ChartId { get; }

    /// <summary>
    ///     Whose words these are — <see cref="Guid.Empty" /> once
    ///     <see cref="TombstoneForPurge" /> has run and the account is gone.
    /// </summary>
    public Guid UserId { get; private set; }

    public CommentAudience Audience { get; }

    /// <summary>Null on a root. Never points at another reply: threads are one level deep.</summary>
    public Guid? ParentCommentId { get; }

    public string Text { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }

    /// <summary>
    ///     The language the author wrote in. Null for the whole of Slice 2 and deliberately not
    ///     guessed: stamping the poster's UI culture would record a Korean speaker browsing in
    ///     English as en-US, and the translation pipeline would then render their comment
    ///     Korean-to-Korean, which is the one rewrite <c>TranslationTarget.ForSource</c> exists to
    ///     prevent. The pivot stage fills it in when that pipeline lands.
    /// </summary>
    public string? SourceLanguage { get; }

    public bool IsRoot => ParentCommentId == null;
    public bool IsDeleted => DeletedAt != null;

    /// <summary>True once a purge cleared the author; the row survives only to hold a thread open.</summary>
    public bool IsTombstoned => IsDeleted && UserId == Guid.Empty;

    public static Comment Post(Guid chartId, Guid userId, CommentAudience audience, string? text,
        DateTimeOffset now)
    {
        return new Comment(Guid.NewGuid(), chartId, RequireUser(userId), audience, null,
            RequireText(text), now, null, null, null, null);
    }

    /// <summary>
    ///     A reply, taking its audience from the root it answers rather than from the caller. That
    ///     is the invariant: a thread cannot be moved between communities, or out of one into
    ///     public, by anything the UI does or fails to do.
    /// </summary>
    public static Comment Reply(Comment root, Guid userId, string? text, DateTimeOffset now)
    {
        if (root.Audience.IsPrivate)
            throw new CommentNotAllowedException("A personal note is not a conversation.");
        // Root plus one level. A reply aimed at a reply is resolved to the root before it gets
        // here, so arriving with one is a caller bug rather than a user's mistake.
        if (!root.IsRoot)
            throw new CommentNotAllowedException("Replies go on the comment that started the thread.");
        if (root.IsDeleted)
            throw new CommentNotAllowedException("That comment is no longer there.");

        return new Comment(Guid.NewGuid(), root.ChartId, RequireUser(userId), root.Audience, root.Id,
            RequireText(text), now, null, null, null, null);
    }

    /// <summary>
    ///     Rehydration from storage. Trusts what it is given: the invariants were enforced on the
    ///     way in, and re-throwing here would make a row written under an older rule unreadable
    ///     rather than merely old.
    /// </summary>
    public static Comment FromStorage(Guid id, Guid chartId, Guid userId, CommentAudience audience,
        Guid? parentCommentId, string text, DateTimeOffset createdAt, DateTimeOffset? editedAt,
        DateTimeOffset? deletedAt, Guid? deletedByUserId, string? sourceLanguage)
    {
        return new Comment(id, chartId, userId, audience, parentCommentId, text, createdAt, editedAt,
            deletedAt, deletedByUserId, sourceLanguage);
    }

    /// <summary>
    ///     Replaces the body and returns what it replaced, so the caller writes the revision row.
    ///     History is retained for moderation — an edit is not a way to make a reported comment
    ///     have never happened.
    /// </summary>
    public string Edit(Guid actorId, string? text, DateTimeOffset now)
    {
        if (actorId != UserId || actorId == Guid.Empty)
            throw new CommentNotAllowedException("You can only edit your own comments.");
        if (IsDeleted)
            throw new CommentNotAllowedException("That comment is no longer there.");

        var previous = Text;
        Text = RequireText(text);
        EditedAt = now;

        return previous;
    }

    public void DeleteByAuthor(Guid actorId, DateTimeOffset now)
    {
        if (actorId != UserId || actorId == Guid.Empty)
            throw new CommentNotAllowedException("You can only delete your own comments.");
        if (IsDeleted) return;

        DeletedAt = now;
        DeletedByUserId = actorId;
    }

    /// <summary>
    ///     Removal by a moderator — the site admin in Slice 2, community admins when moderation
    ///     lands. Remove and only remove: nobody edits anybody else's words, so there is no
    ///     moderator equivalent of <see cref="Edit" /> and there is not meant to be one.
    /// </summary>
    public void RemoveByModerator(Guid moderatorId, DateTimeOffset now)
    {
        // Defence in depth. A moderator cannot see a note in the first place, because the audience
        // filter never returns one to anybody else — this makes that a rule rather than a
        // consequence of a query nobody has broken yet.
        if (Audience.IsPrivate)
            throw new CommentNotAllowedException("Personal notes are not moderated.");
        if (moderatorId == Guid.Empty)
            throw new CommentNotAllowedException("A removal needs a moderator.");
        if (IsDeleted) return;

        DeletedAt = now;
        DeletedByUserId = moderatorId;
    }

    /// <summary>
    ///     What an account purge does to a root that still holds replies: the author goes, the text
    ///     goes, and an anonymous stub keeps the thread's shape. Everything else the account wrote
    ///     is deleted outright — a purge that left rows keyed to a deleted user would be a purge
    ///     that missed some.
    /// </summary>
    public void TombstoneForPurge(DateTimeOffset now)
    {
        UserId = Guid.Empty;
        Text = string.Empty;
        DeletedByUserId = null;
        DeletedAt ??= now;
    }

    /// <summary>
    ///     Throws unless <paramref name="userId" /> may add a vote. Votes live in their own table,
    ///     but the rules about them are the comment's.
    /// </summary>
    public void EnsureCanBeVotedOnBy(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new CommentNotAllowedException("Sign in to vote.");
        if (Audience.IsPrivate)
            throw new CommentNotAllowedException("Personal notes are not voted on.");
        if (IsDeleted)
            throw new CommentNotAllowedException("That comment is no longer there.");
        if (userId == UserId)
            throw new CommentNotAllowedException("You cannot vote on your own comment.");
    }

    /// <summary>
    ///     Whether this row still needs to render once deleted. A stub for a comment nobody
    ///     answered is a headstone in a thread of four, so only a root with replies leaves one.
    /// </summary>
    public bool LeavesStub(bool hasReplies)
    {
        return IsDeleted && IsRoot && hasReplies;
    }

    private static string RequireText(string? text)
    {
        var normalized = CommentText.Normalize(text);
        if (normalized.Length == 0)
            throw new CommentNotAllowedException("Write something first.");
        if (normalized.Length > CommentText.MaxLength)
            throw new CommentNotAllowedException(
                $"Comments are up to {CommentText.MaxLength} characters.");

        return normalized;
    }

    private static Guid RequireUser(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new CommentNotAllowedException("Sign in to comment.");

        return userId;
    }
}
