using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScoreTracker.WeeklyChallenge.Infrastructure.Entities
{
    internal sealed class WeeklyTournamentChartEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid ChartId { get; set; }
        public Guid MixId { get; set; }
        public DateTimeOffset ExpirationDate { get; set; }
    }
}
