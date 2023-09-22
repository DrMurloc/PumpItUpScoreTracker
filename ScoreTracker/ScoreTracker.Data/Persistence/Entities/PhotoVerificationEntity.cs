using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class PhotoVerificationEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid TournamentId { get; set; }
        [Required] public Guid UserId { get; set; }

        [Required] public string PhotoUrl { get; set; } = string.Empty;
    }
}