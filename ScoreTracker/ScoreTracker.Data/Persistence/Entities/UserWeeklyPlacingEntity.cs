using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(UserId))]
    public sealed class UserWeeklyPlacingEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public Guid ChartId { get; set; }
        public int Place { get; set; }
        public DateTimeOffset ObtainedDate { get; set; }
        public bool WasWithinRange { get; set; }
        public int Score { get; set; }
        [MaxLength(32)] public string Plate { get; set; }
        public bool IsBroken { get; set; }
    }
}
