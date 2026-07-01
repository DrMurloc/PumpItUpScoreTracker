using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities;

internal sealed class ChartDifficultyRatingEntity
{
    [Required] [DefaultValue(0)] public double StandardDeviation { get; set; }
    public Guid ChartId { get; set; }

    [Required] public double Difficulty { get; set; }
    [Required] public int Count { get; set; }
    public Guid MixId { get; set; }
}