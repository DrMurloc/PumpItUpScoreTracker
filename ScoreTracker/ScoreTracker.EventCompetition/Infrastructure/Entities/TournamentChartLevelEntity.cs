using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    [Index(nameof(TournamentId))]
    internal sealed class TournamentChartLevelEntity
    {
        [Key] public Guid Id { get; set; }
        public Guid ChartId { get; set; }
        public Guid TournamentId { get; set; }
        public double Level { get; set; }
    }
}
