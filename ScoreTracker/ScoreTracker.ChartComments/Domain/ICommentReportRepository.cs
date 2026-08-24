using ScoreTracker.ChartComments.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Reports and the two queue reads. Vertical-internal: the queue predicates — which slot is
///     open, which reasons escalate — are policy, and nothing outside ChartComments gets to write
///     a second copy of them.
/// </summary>
internal interface ICommentReportRepository
{
    Task Save(CommentReport report, CancellationToken cancellationToken = default);

    Task<CommentReport?> GetById(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>Whether this reporter already has an open report on this comment (either slot).</summary>
    Task<bool> HasOpenFrom(Guid commentId, Guid reporterUserId, CancellationToken cancellationToken = default);

    /// <summary>Every report on one comment with any open slot — what removal resolves.</summary>
    Task<IReadOnlyList<CommentReport>> GetOpenForComment(Guid commentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Community-slot-open reports on comments in the given communities, joined to the comment
    ///     they name. Notes never appear: a note cannot be reported in the first place.
    /// </summary>
    Task<IReadOnlyList<ReportQueueRow>> GetOpenForCommunities(IReadOnlyCollection<Guid> communityIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Site-slot-open reports on the site admin's desk: every report on a public comment, plus
    ///     reports on community comments whose reason escalates.
    /// </summary>
    Task<IReadOnlyList<ReportQueueRow>> GetOpenForSite(CancellationToken cancellationToken = default);
}

/// <summary>
///     A queue row: the report joined to the comment it names. Carries the comment's text because
///     the site admin reads reported contents on the page — an escalated comment lives in a club
///     they need not belong to, and the open report is what grants the read of exactly this one
///     comment.
/// </summary>
internal sealed record ReportQueueRow(
    Guid ReportId,
    Guid CommentId,
    Guid ChartId,
    Guid AuthorUserId,
    Guid? CommunityId,
    string CommentText,
    Guid ReporterUserId,
    CommentReportReason Reason,
    DateTimeOffset ReportedAt,
    string? RenderingLocale = null);
