using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Translations.Infrastructure.Entities;

/// <summary>
///     One queued text. SourceKey is unique — a re-queue replaces the row rather than growing a
///     history — and the text is stored with its link markers already in place, so no URL ever
///     sits in this table or reaches a model. Text length is the comment cap plus marker
///     headroom. The row carries no user key on purpose: whose text this is stays the caller's
///     knowledge, and the caller discards rows here when their originals stop existing.
/// </summary>
[Index(nameof(SourceKey), IsUnique = true)]
[Index(nameof(State), nameof(CreatedAt))]
[Index(nameof(BatchId))]
internal sealed class TranslationRequestEntity
{
    [Key] public Guid Id { get; set; }

    [Required] [MaxLength(200)] public string SourceKey { get; set; } = string.Empty;

    [Required] [MaxLength(1000)] public string Text { get; set; } = string.Empty;

    /// <summary>The <c>TranslationState</c> enum name.</summary>
    [Required] [MaxLength(20)] public string State { get; set; } = string.Empty;

    [MaxLength(20)] public string? SourceLanguage { get; set; }

    /// <summary>
    ///     Stage one's full output, kept but never displayed — it is what makes a bad rendering a
    ///     stage-two re-run instead of a re-translation, and a sixth locale a backfill.
    /// </summary>
    public string? PivotJson { get; set; }

    /// <summary>The batch currently carrying this text, while one is.</summary>
    public Guid? BatchId { get; set; }

    /// <summary>
    ///     When this source key last entered a batch — the submit-side cooldown's clock. Survives
    ///     an upsert on purpose: a text translates at most once per 24 h however often its author
    ///     edits, and this is the fact that enforces it.
    /// </summary>
    public DateTimeOffset? LastSubmittedAt { get; set; }

    [MaxLength(400)] public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
