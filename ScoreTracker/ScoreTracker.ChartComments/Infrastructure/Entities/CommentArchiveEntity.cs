using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;

namespace ScoreTracker.ChartComments.Infrastructure.Entities;

/// <summary>
///     A community comment whose club was deleted (owner standard, 2026-08-14): the words survive
///     the club's death the way archived tables survive a feature's — rows in, nothing out, until
///     a revival wants real data to start from. Every <see cref="CommentEntity" /> column rides
///     for fidelity, plus the club's name snapshot (this table is the only place it still exists)
///     and when the archival happened. Nothing renders these rows.
/// </summary>
// UserId owns the row for purge — words surviving a club's death must not survive their author's
// deletion, and a blanket delete is safe here precisely because nothing renders archives as
// threads, so an orphaned archived reply is inert. DeletedByUserId is a moderator pointer that
// may outlive its account, like everywhere else in this vertical.
[Index(nameof(UserId))]
[PurgeKey(nameof(UserId))]
internal sealed class CommentArchiveEntity
{
    [Key] public Guid Id { get; set; }

    public Guid ChartId { get; set; }

    public Guid UserId { get; set; }

    [Required] [MaxLength(20)] public string Audience { get; set; } = string.Empty;

    public Guid? CommunityId { get; set; }

    /// <summary>The club's name at deletion — its row is gone, so this snapshot is the provenance.</summary>
    [Required] [MaxLength(100)] public string CommunityName { get; set; } = string.Empty;

    public Guid? ParentCommentId { get; set; }

    [Required] [MaxLength(500)] public string Text { get; set; } = string.Empty;

    [MaxLength(20)] public string? SourceLanguage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? EditedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Guid? DeletedByUserId { get; set; }

    public DateTimeOffset ArchivedAt { get; set; }

    /// <summary>Rides along so a revived club's comments keep their seconds.</summary>
    [Precision(9, 3)]
    public decimal? AnchorAt { get; set; }
}
