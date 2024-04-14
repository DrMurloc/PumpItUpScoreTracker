using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class QualifiersConfigurationEntity
    {
        [Key] [Required] public Guid TournamentId { get; set; }
        public string ScoringType { get; set; }
        public string Charts { get; set; }
        public ulong NotificationChannel { get; set; }
        public int ChartPlayCount { get; set; }
    }
}
