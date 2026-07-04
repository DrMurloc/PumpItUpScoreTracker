using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.WeeklyChallenge.Infrastructure.Entities
{
    [PrimaryKey(nameof(ChartId), nameof(MixId))]
    internal sealed class PastTourneyChartsEntity
    {
        public Guid ChartId { get; set; }
        public Guid MixId { get; set; }
        public DateTimeOffset PlayedOn { get; set; }
    }
}
