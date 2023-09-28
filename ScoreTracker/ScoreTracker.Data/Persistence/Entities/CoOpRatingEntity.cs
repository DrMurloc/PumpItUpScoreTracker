using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(ChartId))]
    public sealed class CoOpRatingEntity
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid ChartId { get; set; }
        [Required] public int Player { get; set; }
        [Required] public int Difficulty { get; set; }
    }
}