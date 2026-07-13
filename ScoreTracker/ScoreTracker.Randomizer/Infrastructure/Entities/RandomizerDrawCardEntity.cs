using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Randomizer.Infrastructure.Entities
{
    [Index(nameof(DrawId))]
    internal sealed class RandomizerDrawCardEntity
    {
        // Per-pull identity: protect/veto state hangs off the pull, not the chart, so
        // AllowRepeats and concurrent staff devices are safe by construction.
        [Key] public Guid PullId { get; set; }

        public Guid DrawId { get; set; }
        public Guid ChartId { get; set; }

        // 1-based, stable within a round; ClearVetoed renumbers.
        public int Order { get; set; }

        [Required] public string State { get; set; } = nameof(Contracts.DrawCardState.None);
    }
}
