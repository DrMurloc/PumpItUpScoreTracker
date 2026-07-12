using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(MixId))]
[Index(nameof(Level))]
[Index(nameof(ChartId))]
public sealed class ChartMixEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public Guid MixId { get; set; }

    [Required] public int Level { get; set; }
    public int? NoteCount { get; set; }

    /// <summary>
    ///     Pre-Exceed slot identity ("Crazy", "Another Nightmare", …) — set only on
    ///     legacy-mix rows; the slot is part of chart identity in those eras and its
    ///     numeric Level lives on a per-era scale (docs/design/legacy-mixes.md).
    /// </summary>
    [MaxLength(24)]
    public string? LegacySlot { get; set; }
}