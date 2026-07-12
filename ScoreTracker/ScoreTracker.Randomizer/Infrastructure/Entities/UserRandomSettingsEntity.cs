using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Randomizer.Infrastructure.Entities
{
    [Index(nameof(UserId), nameof(Name))]
    internal sealed class UserRandomSettingsEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] public Guid UserId { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Json { get; set; } = "{}";

        // Mix is a property of the settings, not the page — saved settings list across
        // all mixes with a chip, so nothing "disappears" on the wrong mix.
        [Required] public string Mix { get; set; } = "Phoenix";

        // Set once when the owner shares these settings; anyone with the token can
        // preview and import a copy.
        public Guid? ShareToken { get; set; }
    }
}
