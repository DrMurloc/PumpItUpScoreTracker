using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence
{
    public sealed class UserQualifierHistoryEntity
    {
        public Guid TournamentId { get; set; }
        [Key] public Guid Id { get; set; }
        [Required] public DateTimeOffset RecordedDate { get; set; }

        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Entries { get; set; } = string.Empty;

        public bool IsApproved { get; set; }
    }
}
