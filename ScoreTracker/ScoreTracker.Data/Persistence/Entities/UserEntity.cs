using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class UserEntity
{
    [Key] public Guid Id { get; set; }

    [Required] [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required] public bool IsPublic { get; set; }
}