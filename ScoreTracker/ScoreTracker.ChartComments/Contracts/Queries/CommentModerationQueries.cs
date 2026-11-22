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
///     The site admin's desk: every open report on a public comment, plus escalated reports from
///     inside communities. Site admin only.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetSiteReportedCommentsQuery : IQuery<IReadOnlyList<SiteReportedCommentRecord>>;

/// <summary>
///     The active mutes in one community — the Members page's lift surface. Callable only by
///     someone holding moderation there.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityCommentRestrictionsQuery(Guid CommunityId)
    : IQuery<IReadOnlyList<CommentRestrictionRecord>>;
