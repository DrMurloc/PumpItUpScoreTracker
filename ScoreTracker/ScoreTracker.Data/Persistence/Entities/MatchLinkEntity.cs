using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(TournamentId), nameof(FromMatch), nameof(ToMatch))]
    public sealed class MatchLinkEntity
    {
        [Key] public Guid Id { get; set; }

        [Required] public Guid TournamentId { get; set; }
        public string FromMatch { get; set; } = string.Empty;
        public string ToMatch { get; set; } = string.Empty;
        public int PlayerCount { get; set; }
        public bool IsWinners { get; set; }
        public int Skip { get; set; }
    }
}
