using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities;

public sealed class UserEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public string Name { get; set; } = string.Empty;
}