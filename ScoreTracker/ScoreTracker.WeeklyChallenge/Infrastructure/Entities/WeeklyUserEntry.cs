using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Domain.Records;

namespace ScoreTracker.WeeklyChallenge.Infrastructure.Entities
{
    [Index(nameof(UserId), nameof(ChartId), nameof(MixId), IsUnique = true)]
    internal sealed class WeeklyUserEntry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public Guid ChartId { get; set; }
        public Guid MixId { get; set; }
        public int Score { get; set; }
        public string Plate { get; set; } = string.Empty;
        public bool IsBroken { get; set; }
        public bool WasWithinRange { get; set; }
        public double CompetitiveLevel { get; set; }
        [MaxLength(256)] public string? Photo { get; set; }

        // The trust ladder's source tier (weekly-charts-overhaul.md §7). Existing rows default
        // to Official: historically imports dominated, and the manual path required a photo.
        public ChallengeEntrySource Source { get; set; } = ChallengeEntrySource.Official;
    }
}
