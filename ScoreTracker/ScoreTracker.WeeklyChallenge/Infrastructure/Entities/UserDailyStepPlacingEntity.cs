using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.WeeklyChallenge.Infrastructure.Entities
{
    // Retained per-user Daily Step history, written at each rotation (owner: viewable later on the
    // Weekly-charts rebuild). Mirror of UserWeeklyPlacing + ForDate/IsLimbo.
    [Index(nameof(UserId), nameof(MixId))]
    internal sealed class UserDailyStepPlacingEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public Guid ChartId { get; set; }
        public Guid MixId { get; set; }
        public DateTimeOffset ForDate { get; set; }
        public bool IsLimbo { get; set; }
        public int Place { get; set; }
        public int Score { get; set; }
        [MaxLength(32)] public string Plate { get; set; } = string.Empty;
        public bool IsBroken { get; set; }
        public double CompetitiveLevel { get; set; }
    }
}
