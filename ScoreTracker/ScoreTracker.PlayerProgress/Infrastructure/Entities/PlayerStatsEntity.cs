using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities
{
    [PrimaryKey(nameof(UserId), nameof(MixId))]
    internal sealed class PlayerStatsEntity
    {
        public Guid UserId { get; set; }
        public Guid MixId { get; set; }
        public int TotalRating { get; set; }
        public int HighestLevel { get; set; }
        public int ClearCount { get; set; }
        public int SkillRating { get; set; }
        public double AverageSkillLevel { get; set; }
        public int AverageSkillScore { get; set; }
        public int SinglesRating { get; set; }
        public double AverageSinglesLevel { get; set; }
        public int AverageSinglesScore { get; set; }
        public int DoublesRating { get; set; }
        public double AverageDoublesLevel { get; set; }
        public int AverageDoublesScore { get; set; }
        public int CoOpRating { get; set; }
        public int AverageCoOpScore { get; set; }
        public double CompetitiveLevel { get; set; }
        public double SinglesCompetitiveLevel { get; set; }
        public double DoublesCompetitiveLevel { get; set; }
    }
}
