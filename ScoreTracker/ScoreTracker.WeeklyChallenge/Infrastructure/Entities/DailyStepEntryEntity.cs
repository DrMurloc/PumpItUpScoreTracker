using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.WeeklyChallenge.Infrastructure.Entities
{
    // A player's live entry on today's Daily Step chart. Mirror of WeeklyUserEntry, minus the
    // weekly-only WasWithinRange flag. One row per (user, chart, mix); cleared at rotation.
    [Index(nameof(UserId), nameof(ChartId), nameof(MixId), IsUnique = true)]
    internal sealed class DailyStepEntryEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public Guid ChartId { get; set; }
        public Guid MixId { get; set; }
        public int Score { get; set; }
        [MaxLength(32)] public string Plate { get; set; } = string.Empty;
        public bool IsBroken { get; set; }
        public double CompetitiveLevel { get; set; }
        [MaxLength(256)] public string? Photo { get; set; }
    }
}
