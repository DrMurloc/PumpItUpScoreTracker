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

    /// <summary>
    ///     Judgement breakdown of the play that produced the current best score. All five
    ///     are set together or not at all; NULL = the producing play's breakdown was never
    ///     observed (manual entry, or an import before judgement capture).
    /// </summary>
    public int? Perfects { get; set; }

    public int? Greats { get; set; }
    public int? Goods { get; set; }
    public int? Bads { get; set; }
    public int? Misses { get; set; }
}