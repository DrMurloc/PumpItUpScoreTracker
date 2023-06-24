using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class MixEntity
{
    [Key] public Guid Id { get; set; }

    [Required] [MaxLength(7)] public string Name { get; set; } = string.Empty;
}