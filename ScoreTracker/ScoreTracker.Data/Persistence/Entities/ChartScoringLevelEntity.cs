using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

// Chart Intelligence's projection (rearch F4): the community-derived "effective"
// scoring level, split out of the Catalog-owned ChartMix row.
[Index(nameof(ChartId), nameof(MixId), IsUnique = true)]
public sealed class ChartScoringLevelEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid ChartId { get; set; }

    [Required] public Guid MixId { get; set; }

    [Required] public double ScoringLevel { get; set; }
}
