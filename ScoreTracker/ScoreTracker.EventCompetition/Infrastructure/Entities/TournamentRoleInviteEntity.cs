using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    [Index(nameof(TournamentId))]
    internal sealed class TournamentRoleInviteEntity
    {
        [Key] public Guid Token { get; set; }

        public Guid TournamentId { get; set; }
        [Required] public string Role { get; set; } = string.Empty;
        public DateTimeOffset? ExpiresAt { get; set; }
        public Guid CreatedBy { get; set; }
    }
}
