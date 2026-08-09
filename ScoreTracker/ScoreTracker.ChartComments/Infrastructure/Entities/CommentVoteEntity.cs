using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ChartComments.Infrastructure.Entities;

/// <summary>
///     One thumbs-up. Unique per comment and user, so a double-tap on a slow connection cannot
///     count twice — the constraint is the rule, not a check in a handler.
/// </summary>
[Index(nameof(CommentId), nameof(UserId), IsUnique = true)]
[Index(nameof(UserId))]
internal sealed class CommentVoteEntity
{
    [Key] public Guid Id { get; set; }

    public Guid CommentId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
