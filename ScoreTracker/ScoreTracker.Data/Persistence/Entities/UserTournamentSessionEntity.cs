using Microsoft.EntityFrameworkCore;
using ScoreTracker.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(TournamentId), nameof(UserId))]
    public sealed class UserTournamentSessionEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid UserId { get; set; }
        [Required] public Guid TournamentId { get; set; }

        [Required] public int SessionScore { get; set; }
        [Required] public string ChartEntries { get; set; } = string.Empty;
        [Required] public TimeSpan RestTime { get; set; } = TimeSpan.MinValue;
        [Required] public double AverageDifficulty { get; set; } = 1;
        [Required] public int ChartsPlayed { get; set; } = 0;
        public string? VideoUrl { get; set; }
        [Required] public string VerificationType { get; set; } = SubmissionVerificationType.Unverified.ToString();
        [Required] public bool NeedsApproval { get; set; } = false;
    }
}