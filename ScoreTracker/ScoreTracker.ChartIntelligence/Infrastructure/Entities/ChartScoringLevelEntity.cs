using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.ChartIntelligence.Infrastructure.Entities;

// Chart Intelligence's projection (rearch F4): the community-derived "effective"
// scoring level, split out of the Catalog-owned ChartMix row.
[Index(nameof(ChartId), nameof(MixId), IsUnique = true)]
internal sealed class ChartScoringLevelEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public Guid MixId { get; set; }

    [Required] public double ScoringLevel { get; set; }
}
