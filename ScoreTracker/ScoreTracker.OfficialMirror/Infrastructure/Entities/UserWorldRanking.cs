using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities
{
    [Index(nameof(UserName), nameof(Type))]
    internal sealed class UserWorldRanking
    {
        [Key] public Guid Id { get; set; }
        public Guid MixId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public double AverageLevel { get; set; }
        public int AverageScore { get; set; }
        public int SinglesCount { get; set; }
        public int DoublesCount { get; set; }
        public int TotalRating { get; set; }
        public double CompetitiveLevel { get; set; }
        public double SinglesCompetitive { get; set; }
        public double DoublesCompetitive { get; set; }
    }
}
