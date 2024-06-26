using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(UserId), nameof(ChartId), IsUnique = true)]
    public sealed class WeeklyUserEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public Guid ChartId { get; set; }
        public int Score { get; set; }
        public string Plate { get; set; } = string.Empty;
        public bool IsBroken { get; set; }
        public bool WasWithinRange { get; set; }
        public double CompetitiveLevel { get; set; }
        [MaxLength(256)] public string? Photo { get; set; }
    }
}
