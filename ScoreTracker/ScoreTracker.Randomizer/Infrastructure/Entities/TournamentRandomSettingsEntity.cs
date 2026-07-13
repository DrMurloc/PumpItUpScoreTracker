using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Randomizer.Infrastructure.Entities
{
    [Index(nameof(TournamentId), nameof(Name))]
    internal sealed class TournamentRandomSettingsEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] public Guid TournamentId { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Json { get; set; } = "{}";
        [Required] public string Mix { get; set; } = "Phoenix";
    }
}
