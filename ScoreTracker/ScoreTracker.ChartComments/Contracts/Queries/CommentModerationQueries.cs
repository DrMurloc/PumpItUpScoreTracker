using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.ChartComments.Contracts.Queries;

/// <summary>
///     The community moderator's open reports, across every club where the caller holds
///     ModerateComments (or the creator seat), hierarchy-filtered — an admin never sees a report
///     they could not act on, so a report against a fellow admin's comment waits for the creator.
///     Optionally narrowed to one community for that club's admin page panel.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOpenCommentReportsQuery(Guid? CommunityId = null)
    : IQuery<IReadOnlyList<ReportedCommentRecord>>;

/// <summary>
///     One community-queue row. Chart display data (bubble, jacket) is resolved in Web via
///     Catalog — this vertical stores a ChartId and knows nothing about charts.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ReportedCommentRecord(
    Guid ReportId,
    Guid CommentId,
    Guid ChartId,
    Guid CommunityId,
    Name? CommunityName,
    Guid ReportedUserId,
    Name? ReportedUserName,
    Name? ReporterName,
    CommentReportReason Reason,
    DateTimeOffset ReportedAt);

/// <summary>
///     The site admin's desk: every open report on a public comment, plus escalated reports from
///     inside communities. Site admin only.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSiteReportedCommentsQuery : IQuery<IReadOnlyList<SiteReportedCommentRecord>>;

/// <summary>
///     One site-queue row, carrying the comment's parsed body: an escalated comment lives in a
///     club the site admin need not belong to, and the open report is what grants the read of
///     exactly this one comment — on the page, because the dialog would offer no scope chip for
///     it. Spans rather than a string, like every body that leaves this vertical. A null
///     <see cref="CommunityName" /> means the comment is public.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record SiteReportedCommentRecord(
    Guid ReportId,
    Guid CommentId,
    Guid ChartId,
    Guid? CommunityId,
    Name? CommunityName,
    Guid ReportedUserId,
    Name? ReportedUserName,
    Name? ReporterName,
    CommentReportReason Reason,
    DateTimeOffset ReportedAt,
    IReadOnlyList<CommentSpan> Body,
    /// <summary>The rendering locale the reporter was reading, null when they read the original.</summary>
    string? ReporterSawLocale = null,
    /// <summary>
    ///     What the reporter actually saw, when it was a rendering — beside the original, never
    ///     instead of it, because the language-asymmetry attack is only visible with both ends on
    ///     the page. Null when they read the original or the rendering has since been replaced.
    /// </summary>
    IReadOnlyList<CommentSpan>? ReporterSawBody = null);

/// <summary>
///     The active mutes in one community — the Members page's lift surface. Callable only by
///     someone holding moderation there.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityCommentRestrictionsQuery(Guid CommunityId)
    : IQuery<IReadOnlyList<CommentRestrictionRecord>>;

[ExcludeFromCodeCoverage]
public sealed record CommentRestrictionRecord(
    Guid UserId,
    Name? UserName,
    Guid RestrictedByUserId,
    Name? RestrictedByName,
    string? Reason,
    DateTimeOffset CreatedAt);
