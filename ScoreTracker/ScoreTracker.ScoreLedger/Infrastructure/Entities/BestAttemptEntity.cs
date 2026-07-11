using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ScoreLedger.Infrastructure.Entities;

[Index(nameof(UserId), nameof(ChartId), nameof(MixId))]
internal sealed class BestAttemptEntity
{
    [Key] public Guid Id { get; set; }
    [Required] public Guid UserId { get; set; }

    [Required] public Guid ChartId { get; set; }

    /// <summary>
    ///     Which mix the attempt was played on. Defaults to XX (the table's original,
    ///     implicit scope) — the PhoenixRecordsPerMix precedent, so pre-existing rows
    ///     stay valid. Legacy-mix recording writes one best attempt per (user, chart, mix).
    /// </summary>
    [Required]
    public Guid MixId { get; set; }

    [Required] public DateTimeOffset RecordedDate { get; set; }

    [Required] public string LetterGrade { get; set; } = string.Empty;
    [Required] public bool IsBroken { get; set; }
    public int? Score { get; set; }
}