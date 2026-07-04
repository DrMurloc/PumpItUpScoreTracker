using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    internal sealed class QualifiersConfigurationEntity
    {
        [Key] [Required] public Guid TournamentId { get; set; }
        public Guid MixId { get; set; }
        public string ScoringType { get; set; }
        public string Charts { get; set; }
        public bool AllCharts { get; set; }
        public ulong NotificationChannel { get; set; }
        public int ChartPlayCount { get; set; }
        public DateTimeOffset? CutoffTime { get; set; }
    }
}
