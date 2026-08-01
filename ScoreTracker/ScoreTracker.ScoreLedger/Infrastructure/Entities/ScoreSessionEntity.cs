using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ScoreLedger.Infrastructure.Entities;

// One play session or import run. SessionId existed before this table as a bare grouping
// key minted in memory by the batch accumulator; the table is what gives it a wall-clock
// time, because the journal's OccurredAt is the *site's* play date and cannot answer "what
// did I import on Tuesday" (docs/design/delete-my-data.md §4).
[Index(nameof(UserId), nameof(StartedAt))]
internal sealed class ScoreSessionEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid UserId { get; set; }

    [Required] public Guid MixId { get; set; }

    /// <summary>Acquisition channel: manual | officialImport | csv.</summary>
    [Required]
    [MaxLength(32)]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    ///     Which card an official import pulled from — the answer to "I imported the wrong
    ///     card", which is the phrase this whole feature exists for. Null on every other source.
    /// </summary>
    [MaxLength(100)]
    public string? AccountTag { get; set; }

    [MaxLength(100)] public string? CardId { get; set; }

    /// <summary>Wall clock, not the site's play date. That distinction is the point of the table.</summary>
    [Required]
    public DateTimeOffset StartedAt { get; set; }

    [Required] public DateTimeOffset LastActivityAt { get; set; }

    /// <summary>
    ///     Records changed by this session, split by kind. Non-best observed plays are journaled
    ///     but never counted here — they changed nothing.
    /// </summary>
    public int ScoreCount { get; set; }

    public int NewCount { get; set; }
    public int UpscoreCount { get; set; }
}
