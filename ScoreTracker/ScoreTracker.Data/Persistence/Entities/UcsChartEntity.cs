using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(PiuGameId), IsUnique = true)]
    public sealed class UcsChartEntity
    {
        public int PiuGameId { get; set; }
        [Key] public Guid Id { get; set; }
        [MaxLength(32)] public string ChartType { get; set; }
        public int Level { get; set; }
        public Guid SongId { get; set; }
        public string Uploader { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
