using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(UserName), nameof(Type))]
    public sealed class UserWorldRanking
    {
        [Key] public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public double AverageLevel { get; set; }
        public int AverageScore { get; set; }
        public int SinglesCount { get; set; }
        public int DoublesCount { get; set; }
        public int TotalRating { get; set; }
    }
}
