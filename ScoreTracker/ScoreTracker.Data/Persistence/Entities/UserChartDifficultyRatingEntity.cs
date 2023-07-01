using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(UserId), nameof(ChartId))]
[Index(nameof(ChartId))]
public sealed class UserChartDifficultyRatingEntity
{
    public Guid Id { get; set; }

    [Required] public Guid UserId { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public int Scale { get; set; }
    public Guid MixId { get; set; }
}