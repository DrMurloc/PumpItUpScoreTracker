using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(MixId), nameof(ChartId))]
    public sealed class ChartPreferenceRatingEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid ChartId { get; set; }
        [Required] public Guid MixId { get; set; }
        [Required] public decimal Rating { get; set; }
        [Required] public int Count { get; set; }
    }
}
