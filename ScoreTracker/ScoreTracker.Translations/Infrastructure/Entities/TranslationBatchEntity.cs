using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Translations.Infrastructure.Entities;

/// <summary>
///     One submitted provider batch — the poll list while open, the spend ledger once complete.
///     Usage lands as the four token counts plus the dollars they priced to at completion, so the
///     rolling ceiling reads recorded fact rather than re-deriving from prices that may have
///     changed since.
/// </summary>
[Index(nameof(CompletedAt))]
internal sealed class TranslationBatchEntity
{
    [Key] public Guid Id { get; set; }

    [Required] [MaxLength(100)] public string ProviderBatchId { get; set; } = string.Empty;

    /// <summary>Which stage this batch ran — the <c>PivotSubmitted</c> or <c>FanOutSubmitted</c> name.</summary>
    [Required] [MaxLength(20)] public string Stage { get; set; } = string.Empty;

    public int ItemCount { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long CacheCreationInputTokens { get; set; }

    public long CacheReadInputTokens { get; set; }

    [Precision(9, 4)] public decimal CostUsd { get; set; }
}
