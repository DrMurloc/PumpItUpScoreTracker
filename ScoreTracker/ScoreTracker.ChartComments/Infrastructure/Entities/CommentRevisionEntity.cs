using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ChartComments.Infrastructure.Entities;

/// <summary>
///     A body an edit replaced, retained so moderation can see what a comment said when it was
///     reported. Carries no user key on purpose — the author is on the comment, and duplicating it
///     here would give a purge two places to be right about.
/// </summary>
/// <remarks>
///     That absence is why a purge reaches this table by <see cref="CommentId" />: nothing keyed on
///     a user would ever find these rows, and they hold the exact text the purge exists to remove.
/// </remarks>
[Index(nameof(CommentId))]
internal sealed class CommentRevisionEntity
{
    [Key] public Guid Id { get; set; }

    public Guid CommentId { get; set; }

    [Required] [MaxLength(500)] public string Text { get; set; } = string.Empty;

    public DateTimeOffset ReplacedAt { get; set; }
}
