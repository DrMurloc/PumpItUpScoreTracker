using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Catalog.Infrastructure.Entities
{
    /// <summary>Composite PK (ChartId, Source, MetricName) pinned in <see cref="Wiring.CatalogModelContribution" />.</summary>
    [Index(nameof(Source))]
    internal sealed class ChartSkillMetricEntity
    {
        public Guid ChartId { get; set; }
        [Required] [MaxLength(32)] public string Source { get; set; } = string.Empty;
        [Required] [MaxLength(64)] public string MetricName { get; set; } = string.Empty;
        [Precision(9, 4)] public decimal Value { get; set; }
        [MaxLength(16)] public string? Grade { get; set; }
    }
}
