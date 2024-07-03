using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class CoOpPlayerEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid TournamentId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string? CoOpTitle { get; set; }
        public string? DifficultyTitle { get; set; }
        public bool IsInTeam { get; set; }
    }
}
