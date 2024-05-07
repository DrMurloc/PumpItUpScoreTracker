using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class ChartBountyEntity
    {
        [Key] public Guid ChartId { get; set; }
        public int Worth { get; set; }
    }
}
