using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ScoreLedger.Infrastructure.Entities;

[Index(nameof(UserId), nameof(ChartId), nameof(MixId), IsUnique = true)]
[Index(nameof(ChartId))]
internal sealed class PhoenixRecordEntity
{
    [Key] public Guid Id { get; set; }
    [Required] public Guid UserId { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public Guid MixId { get; set; }

    [Required] public DateTimeOffset RecordedDate { get; set; }
    public int? Score { get; set; }
    public string? LetterGrade { get; set; } = string.Empty;
    public string? Plate { get; set; } = string.Empty;
    [Required] public bool IsBroken { get; set; }

    /// <summary>
    ///     Acquisition channel of the CURRENT best (manual | officialImport | csv).
    ///     Verified ⇔ officialImport; NULL = predates capture (treated unverified).
    /// </summary>
    [MaxLength(32)]
    public string? Source { get; set; }
}