using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class DiscordLoginEntity
{
    [Key] public ulong DiscordId { get; set; }

    [Required] public Guid UserId { get; set; }
}