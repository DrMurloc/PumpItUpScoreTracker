using System.ComponentModel.DataAnnotations;
using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class TournamentEntity
    {
        [Key] [Required] public Guid Id { get; set; }

        [Required] public string Name { get; set; } = string.Empty;

        [Required] public string Configuration { get; set; } = string.Empty;
        [Required] public string Type { get; set; } = TournamentType.Stamina.ToString();
        [Required] public string Location { get; set; } = "Remote";
        public bool IsHighlighted { get; set; } = false;
        public string? LinkOverride { get; set; } = null;
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
    }
}