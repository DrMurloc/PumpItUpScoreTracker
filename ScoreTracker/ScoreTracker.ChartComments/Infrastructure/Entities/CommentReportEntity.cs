using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Infrastructure.Entities;

/// <summary>
///     One player's report of one comment, with a resolution slot per queue. Reason is the enum
///     name; RenderingLocale is what the reporter was reading, null until translation exists.
/// </summary>
// ReporterUserId owns the row — a report is the reporter's data, and their purge takes it (an
// open report vanishing with its reporter is accepted). The two resolver columns are moderators,
// different people, and may outlive their accounts like DeletedByUserId does.
[Index(nameof(CommentId))]
[Index(nameof(ReporterUserId))]
[PurgeKey(nameof(ReporterUserId))]
internal sealed class CommentReportEntity
{
    [Key] public Guid Id { get; set; }

    public Guid CommentId { get; set; }

    public Guid ReporterUserId { get; set; }

    [Required] [MaxLength(40)] public string Reason { get; set; } = string.Empty;

    [MaxLength(20)] public string? RenderingLocale { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CommunityResolvedAt { get; set; }

    public Guid? CommunityResolvedByUserId { get; set; }

    public DateTimeOffset? SiteResolvedAt { get; set; }

    public Guid? SiteResolvedByUserId { get; set; }
}
