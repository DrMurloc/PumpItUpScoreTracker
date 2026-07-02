using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.WeeklyChallenge.Infrastructure.Entities
{
    internal sealed class PastTourneyChartsEntity
    {
        [Key] public Guid ChartId { get; set; }
        public DateTimeOffset PlayedOn { get; set; }
    }
}
