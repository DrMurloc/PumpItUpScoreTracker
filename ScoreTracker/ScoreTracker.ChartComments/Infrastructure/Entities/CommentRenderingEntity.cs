using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ChartComments.Infrastructure.Entities;

/// <summary>
///     One locale's rendering of one comment, stored after link substitution — real URLs, ready to
///     parse like any body. Renderings are derived data and die with their comment: purge, archive
///     and hard delete all cascade here by CommentId, and an edit clears them (the text they
///     rendered no longer exists). No user key on purpose — whose words these are is the comment's
///     knowledge — which is why the purge reaches these rows by comment id, like revisions.
/// </summary>
[Index(nameof(CommentId), nameof(Locale), IsUnique = true)]
internal sealed class CommentRenderingEntity
{
    [Key] public Guid Id { get; set; }

    public Guid CommentId { get; set; }

    [Required] [MaxLength(20)] public string Locale { get; set; } = string.Empty;

    /// <summary>Korean of a 500-character Latin comment can run long; headroom is cheap.</summary>
    [Required] [MaxLength(2000)] public string Text { get; set; } = string.Empty;

    /// <summary>Provenance — model and path — so "why do these two read differently?" has an answer.</summary>
    [Required] [MaxLength(200)] public string TranslatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
