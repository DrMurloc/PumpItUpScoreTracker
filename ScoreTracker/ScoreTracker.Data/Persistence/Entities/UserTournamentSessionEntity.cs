using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

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
    }
}