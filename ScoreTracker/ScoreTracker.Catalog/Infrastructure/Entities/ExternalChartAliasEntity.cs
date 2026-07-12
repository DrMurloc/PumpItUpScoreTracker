using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Catalog.Infrastructure.Entities
{
    [Index(nameof(Source), nameof(ExternalKey), IsUnique = true)]
    [Index(nameof(Source), nameof(Status))]
    internal sealed class ExternalChartAliasEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] [MaxLength(32)] public string Source { get; set; } = string.Empty;
        [Required] [MaxLength(200)] public string ExternalKey { get; set; } = string.Empty;
        public Guid? ChartId { get; set; }
        [Required] [MaxLength(16)] public string Status { get; set; } = string.Empty;
        public DateTimeOffset LastCheckedAt { get; set; }
    }
}
