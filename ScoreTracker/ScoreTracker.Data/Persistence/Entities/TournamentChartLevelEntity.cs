using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(TournamentId))]
    public sealed class TournamentChartLevelEntity
    {
        [Key] public Guid Id { get; set; }
        public Guid ChartId { get; set; }
        public Guid TournamentId { get; set; }
        public double Level { get; set; }
    }
}
