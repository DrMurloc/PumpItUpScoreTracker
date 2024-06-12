using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(TournamentId))]
    public sealed class TournamentPlayerEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public int Id { get; set; }

        public Guid TournamentId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int Seed { get; set; }
        public ulong? DiscordId { get; set; }
        public string Notes { get; set; } = string.Empty;
        public bool PotentialConflict { get; set; } = false;
    }
}
