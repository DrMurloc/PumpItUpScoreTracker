using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities
{
    internal sealed class OfficialLeaderboardImportStateEntity
    {
        // One row per mix, keyed by the scores.Mix row id.
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid MixId { get; set; }

        [Required] public DateTimeOffset LastImportedAt { get; set; }
    }
}
