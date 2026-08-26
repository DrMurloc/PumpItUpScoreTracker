using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Catalog.Infrastructure.Entities
{
    /// <summary>
    ///     A computed cache, not a source of truth: every row is rederivable from the banked
    ///     metrics and the mix's catalog. Composite PK (MixId, ChartType, Level, Badge) is
    ///     pinned in <see cref="Wiring.CatalogModelContribution" />.
    /// </summary>
    internal sealed class ChartFolderBaselineEntity
    {
        public Guid MixId { get; set; }
        [Required] [MaxLength(16)] public string ChartType { get; set; } = string.Empty;
        public int Level { get; set; }
        [Required] [MaxLength(64)] public string Badge { get; set; } = string.Empty;
        [Precision(9, 4)] public decimal CoreCutoff { get; set; }
        [Precision(9, 4)] public decimal DrenchedCutoff { get; set; }
        [Precision(9, 4)] public decimal PresenceCutoff { get; set; }
        public int PresentCount { get; set; }
        public int AnalyzedCharts { get; set; }
    }
}
