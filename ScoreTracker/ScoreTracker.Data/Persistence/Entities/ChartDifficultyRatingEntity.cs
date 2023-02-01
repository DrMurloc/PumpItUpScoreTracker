using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class ChartDifficultyRatingEntity
{
    [Key] public Guid ChartId { get; set; }

    [Required] public double Difficulty { get; set; }
    [Required] public int Count { get; set; }
}