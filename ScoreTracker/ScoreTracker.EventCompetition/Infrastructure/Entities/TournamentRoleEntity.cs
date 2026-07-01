using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    [Index(nameof(TournamentId))]
    internal sealed class TournamentRoleEntity
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public Guid TournamentId { get; set; }
        public string Role { get; set; } = string.Empty;
    }
}
