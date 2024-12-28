using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(UserId), nameof(ChartId), IsUnique = true)]
    public sealed class PhoenixRecordStatsEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public Guid ChartId { get; set; }

        public double Pumbility { get; set; }
        public double PumbilityPlus { get; set; }
    }
}
