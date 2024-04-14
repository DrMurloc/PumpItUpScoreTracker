using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence
{
    [Index(nameof(TournamentId))]
    public sealed class UserQualifierEntity
    {
        public Guid TournamentId { get; set; }
        [Key] public Guid Id { get; set; }

        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Entries { get; set; } = string.Empty;

        public bool IsApproved { get; set; }
    }
}
