using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class SongEntity
{
    [Required] public string Name { get; set; } = string.Empty;

    [Key] public Guid Id { get; set; }
}