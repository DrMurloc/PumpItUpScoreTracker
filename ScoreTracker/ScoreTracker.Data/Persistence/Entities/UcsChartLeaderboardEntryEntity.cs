using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(ChartId), nameof(UserId), IsUnique = true)]
    public sealed class UcsChartLeaderboardEntryEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public Guid ChartId { get; set; }

        public int Score { get; set; }
        [MaxLength(32)] public string Plate { get; set; } = string.Empty;
        public bool IsBroken { get; set; }
        [MaxLength(128)] public string? VideoPath { get; set; }
        [MaxLength(128)] public string? ImageUrl { get; set; }
        public DateTimeOffset RecordedOn { get; set; }
    }
}
