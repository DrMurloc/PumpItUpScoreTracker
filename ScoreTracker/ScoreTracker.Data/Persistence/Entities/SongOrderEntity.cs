using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class SongOrderEntity
{
    [Key] public Guid SongId { get; set; }

    [Required] public int Order { get; set; }
}