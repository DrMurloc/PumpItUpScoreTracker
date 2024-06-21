using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class PastTourneyChartsEntity
    {
        [Key] public Guid ChartId { get; set; }
        public DateTimeOffset PlayedOn { get; set; }
    }
}
