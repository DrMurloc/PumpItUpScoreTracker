using ScoreTracker.ChartComments.Contracts;
using ScoreTracker.Domain.Exceptions;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     One player's report of one comment. Resolution is per-queue, and that is the point of the
///     two stamp pairs: a community admin's dismissal clears their panel and only theirs, while an
///     escalated report stays on the site admin's desk until the site admin acts — escalation
///     exists precisely for the club that won't. Removal resolves every open slot at once, because
///     a removed comment leaves nothing to act on in either queue.
/// </summary>
internal sealed class CommentReport
{
    private CommentReport(CommentReportState state)
    {
        Id = state.Id;
        CommentId = state.CommentId;
        ReporterUserId = state.ReporterUserId;
        Reason = state.Reason;
        RenderingLocale = state.RenderingLocale;
        CreatedAt = state.CreatedAt;
        CommunityResolvedAt = state.CommunityResolvedAt;
        CommunityResolvedByUserId = state.CommunityResolvedByUserId;
        SiteResolvedAt = state.SiteResolvedAt;
        SiteResolvedByUserId = state.SiteResolvedByUserId;
    }

    public Guid Id { get; }
    public Guid CommentId { get; }
    public Guid ReporterUserId { get; }
    public CommentReportReason Reason { get; }

    /// <summary>
    ///     The rendering the reporter was reading — null until the translation pipeline exists,
    ///     and null forever for a reader of the original. Stored because translation launders the
    ///     thing being detected: a moderator reading ko-KR cannot evaluate a report filed against
    ///     the es-ES rendering without knowing that is what was read.
    /// </summary>
    public string? RenderingLocale { get; }

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? CommunityResolvedAt { get; private set; }
    public Guid? CommunityResolvedByUserId { get; private set; }
    public DateTimeOffset? SiteResolvedAt { get; private set; }
    public Guid? SiteResolvedByUserId { get; private set; }

    public bool IsOpenForCommunity => CommunityResolvedAt == null;
    public bool IsOpenForSite => SiteResolvedAt == null;

    public static CommentReport File(Guid commentId, Guid reporterUserId, CommentReportReason reason,
        string? renderingLocale, DateTimeOffset now)
    {
        if (reporterUserId == Guid.Empty)
            throw new CommentNotAllowedException("Sign in to report a comment.");

        return new CommentReport(new CommentReportState(Guid.NewGuid(), commentId, reporterUserId,
            reason, renderingLocale, now));
    }

    /// <summary>Rehydration from storage — trusts what it is given, like <see cref="Comment.FromStorage" />.</summary>
    public static CommentReport FromStorage(CommentReportState state)
    {
        return new CommentReport(state);
    }

    /// <summary>
    ///     A community moderator's dismissal. Idempotent — two moderators racing on the same row
    ///     leaves the first stamp standing, which is the answer to "was this handled" either way.
    /// </summary>
    public void ResolveForCommunity(Guid moderatorId, DateTimeOffset now)
    {
        RequireResolver(moderatorId);
        if (CommunityResolvedAt != null) return;

        CommunityResolvedAt = now;
        CommunityResolvedByUserId = moderatorId;
    }

    /// <summary>The site admin's dismissal — their slot and only theirs.</summary>
    public void ResolveForSite(Guid adminId, DateTimeOffset now)
    {
        RequireResolver(adminId);
        if (SiteResolvedAt != null) return;

        SiteResolvedAt = now;
        SiteResolvedByUserId = adminId;
    }

    /// <summary>
    ///     What removal does: closes every still-open slot at once, keeping whichever stamps
    ///     already exist. A removed comment leaves nothing to act on in any queue.
    /// </summary>
    public void ResolveEverywhere(Guid actorId, DateTimeOffset now)
    {
        ResolveForCommunity(actorId, now);
        ResolveForSite(actorId, now);
    }

    private static void RequireResolver(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new CommentNotAllowedException("A resolution needs a moderator.");
    }
}
