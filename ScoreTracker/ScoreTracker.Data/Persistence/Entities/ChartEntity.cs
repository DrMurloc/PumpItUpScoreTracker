using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class ChartEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid SongId { get; set; }

    [Required] public int Level { get; set; }

    [Required] public string Type { get; set; } = string.Empty;
}