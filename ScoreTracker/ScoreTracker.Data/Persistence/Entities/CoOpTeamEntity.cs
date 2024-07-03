using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(TournamentId))]
    public sealed class CoOpTeamEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid TournamentId { get; set; }
        public string TeamName { get; set; }
        public string Player1Name { get; set; }
        public string Player2Name { get; set; }
        public int? Seed { get; set; }
    }
}
