using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class PlayerStatsEntity
    {
        [Key] public Guid UserId { get; set; }
        public int TotalRating { get; set; }
        public int SkillRating { get; set; }
        public int SinglesRating { get; set; }
        public int DoublesRating { get; set; }
        public int CoOpRating { get; set; }
    }
}
