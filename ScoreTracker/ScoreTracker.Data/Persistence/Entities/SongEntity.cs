using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities;

[Index(nameof(Name))]
public sealed class SongEntity
{
    [Required] public string Name { get; set; } = string.Empty;

    [Key] public Guid Id { get; set; }
    [Required] public string ImagePath { get; set; } = string.Empty;
    [Required] [MaxLength(8)] public string Type { get; set; } = "Arcade";

    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(0);
}