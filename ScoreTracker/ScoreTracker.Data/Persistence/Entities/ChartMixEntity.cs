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
    public double? ScoringLevel { get; set; }
    public int? NoteCount { get; set; }
}