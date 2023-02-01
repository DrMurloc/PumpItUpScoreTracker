using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(Level))]
[Index(nameof(Type))]
[Index(nameof(SongId))]
public sealed class ChartEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid SongId { get; set; }

    [Required] public int Level { get; set; }

    [Required] public string Type { get; set; } = string.Empty;
    public ChartDifficultyRatingEntity? DifficultyRating { get; set; }
}