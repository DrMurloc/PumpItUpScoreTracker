using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities
{
    [Index(nameof(UserId), nameof(MixId))]
    internal sealed class PlayerHistoryEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public Guid MixId { get; set; }
        public DateTimeOffset Date { get; set; }
        public double CompetitiveLevel { get; set; }
        public double SinglesLevel { get; set; }
        public double DoublesLevel { get; set; }
        public int CoOpRating { get; set; }
        public int PassCount { get; set; }
        public int? SkillRating { get; set; }
    }
}
