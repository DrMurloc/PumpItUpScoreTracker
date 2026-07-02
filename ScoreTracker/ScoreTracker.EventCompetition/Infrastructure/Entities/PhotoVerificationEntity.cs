using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    internal sealed class PhotoVerificationEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid TournamentId { get; set; }
        [Required] public Guid UserId { get; set; }

        [Required] public string PhotoUrl { get; set; } = string.Empty;
    }
}