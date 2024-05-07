using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class BountyLeaaderboardEntity
    {
        [Key] public Guid UserId { get; set; }
        public int MonthlyTotal { get; set; }
        public int Total { get; set; }
    }
}
