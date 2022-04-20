using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class DiscordLoginEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public ulong DiscordId { get; set; }

    [Required] public Guid UserId { get; set; }
}