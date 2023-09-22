using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Domain.Enums;

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