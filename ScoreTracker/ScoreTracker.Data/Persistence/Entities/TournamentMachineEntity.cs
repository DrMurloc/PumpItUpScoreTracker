using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(TournamentId))]
    public sealed class TournamentMachineEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public Guid TournamentId { get; set; }
        [MaxLength(32)] public string MachineName { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool IsWarmup { get; set; }
    }
}
