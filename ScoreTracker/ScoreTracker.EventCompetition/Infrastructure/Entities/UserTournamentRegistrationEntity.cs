using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    [Index(nameof(TournamentId), nameof(UserId), IsUnique = true)]
    internal sealed class UserTournamentRegistrationEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid TournamentId { get; set; }
        public Guid UserId { get; set; }
    }
}
