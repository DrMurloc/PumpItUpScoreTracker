using ScoreTracker.ChartComments.Contracts;

namespace ScoreTracker.ChartComments.Domain;

/// <summary>
///     Everything a <see cref="CommentReport" /> is made of, in one parameter — the same shape
///     <see cref="CommentState" /> takes, and for the same reason: the four resolution fields are
///     two nullable timestamp/user pairs, and naming them at the call site is what keeps a
///     community stamp out of a site slot.
/// </summary>
internal sealed record CommentReportState(
    Guid Id,
    Guid CommentId,
    Guid ReporterUserId,
    CommentReportReason Reason,
    string? RenderingLocale,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CommunityResolvedAt = null,
    Guid? CommunityResolvedByUserId = null,
    DateTimeOffset? SiteResolvedAt = null,
    Guid? SiteResolvedByUserId = null);
