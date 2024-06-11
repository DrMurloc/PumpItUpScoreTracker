using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(UserId), nameof(Name))]
    public sealed class UserRandomSettingsEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] public Guid UserId { get; set; }
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Json { get; set; } = "{}";
    }
}
