using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class OfficialLeaderboardImportStateEntity
    {
        // Single-row table: Id is always 1, set explicitly so we never insert duplicates.
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [Required] public DateTimeOffset LastImportedAt { get; set; }
    }
}
