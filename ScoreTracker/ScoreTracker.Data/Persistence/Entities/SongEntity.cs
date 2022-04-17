using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(Name))]
public sealed class SongEntity
{
    [Required] public string Name { get; set; } = string.Empty;

    [Key] public Guid Id { get; set; }
}