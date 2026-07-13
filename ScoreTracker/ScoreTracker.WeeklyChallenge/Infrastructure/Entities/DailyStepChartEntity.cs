using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.WeeklyChallenge.Infrastructure.Entities
{
    // The one live Daily Step chart per mix (0–1 rows). Cleared and re-drawn each midnight-ET
    // rotation; ForDate is the day it represents, IsLimbo flips the scoring direction.
    [Index(nameof(MixId))]
    internal sealed class DailyStepChartEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid ChartId { get; set; }
        public Guid MixId { get; set; }
        public DateTimeOffset ForDate { get; set; }
        public bool IsLimbo { get; set; }
        public DateTimeOffset ExpirationDate { get; set; }
    }
}
