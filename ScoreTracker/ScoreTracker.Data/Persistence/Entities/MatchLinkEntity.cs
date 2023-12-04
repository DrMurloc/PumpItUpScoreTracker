using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(FromMatch))]
    [Index(nameof(ToMatch))]
    public sealed class MatchLinkEntity
    {
        [Key] public Guid Id { get; set; }

        public string FromMatch { get; set; } = string.Empty;
        public string ToMatch { get; set; } = string.Empty;
        public int PlayerCount { get; set; }
        public bool IsWinners { get; set; }
    }
}
