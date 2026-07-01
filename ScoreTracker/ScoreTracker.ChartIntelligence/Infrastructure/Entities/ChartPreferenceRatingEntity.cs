using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities
{
    [Index(nameof(MixId), nameof(ChartId))]
    internal sealed class ChartPreferenceRatingEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid ChartId { get; set; }
        [Required] public Guid MixId { get; set; }
        [Required] public decimal Rating { get; set; }
        [Required] public int Count { get; set; }
    }
}
