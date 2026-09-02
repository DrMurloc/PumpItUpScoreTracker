using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ChartComments.Infrastructure.Entities;

/// <summary>
///     One comment, reply or personal note. Audience is stored as its enum name plus the community
///     id, so a club that renames does not strand its threads.
/// </summary>
// The read path always asks the same question: this chart, this audience. Private rows sit in the
// same index as everything else, which is exactly why the audience predicate belongs in the
// repository rather than in a caller that might forget it.
[Index(nameof(ChartId), nameof(Audience), nameof(CommunityId))]
[Index(nameof(ParentCommentId))]
[Index(nameof(UserId))]
internal sealed class CommentEntity
{
    [Key] public Guid Id { get; set; }

    public Guid ChartId { get; set; }

    /// <summary>
    ///     Whose words these are, or <see cref="Guid.Empty" /> on a purge tombstone. Deliberately
    ///     NOT in the vertical's UserOwned manifest: a blanket delete by this column would orphan
    ///     every reply to a purged root, and the row count would still look right.
    /// </summary>
    public Guid UserId { get; set; }

    [Required] [MaxLength(20)] public string Audience { get; set; } = string.Empty;

    /// <summary>Set only on a community audience.</summary>
    public Guid? CommunityId { get; set; }

    /// <summary>Null on a root. Never points at another reply — threads are one level deep.</summary>
    public Guid? ParentCommentId { get; set; }

    [Required] [MaxLength(500)] public string Text { get; set; } = string.Empty;

    /// <summary>
    ///     The language the author wrote in, filled in by the translation pivot when that lands.
    ///     Null for the whole of this slice and deliberately not guessed — see the aggregate.
    /// </summary>
    [MaxLength(20)]
    public string? SourceLanguage { get; set; }

    /// <summary>
    ///     When this text last went to the translation pipeline — the edit-requeue cooldown's
    ///     clock. Null until the first queue, and for notes always.
    /// </summary>
    public DateTimeOffset? TranslationQueuedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    ///     Who deleted it — the author, or a moderator. A second user key, which is why the purge
    ///     for this table is hand-written rather than convention-resolved.
    /// </summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>
    ///     The second of the chart the comment points at, or null for a comment about the whole
    ///     chart (docs/design/step-chart-comments D1). Null on every reply — a reply reads its
    ///     root's. Three decimals: the step payload's row times carry milliseconds.
    /// </summary>
    [Precision(9, 3)]
    public decimal? AnchorAt { get; set; }
}
