using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class TournamentEntity
    {
        [Key] [Required] public Guid Id { get; set; }

        [Required] public string Name { get; set; } = string.Empty;

        [Required] public string Configuration { get; set; } = string.Empty;

        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
    }
}