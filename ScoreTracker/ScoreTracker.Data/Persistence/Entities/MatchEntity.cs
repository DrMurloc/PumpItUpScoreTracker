using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(Name))]
    public sealed class MatchEntity
    {
        [Key] public Guid Id { get; set; }

        [Required] public string Name { get; set; } = string.Empty;

        [Required] public string Json { get; set; } = string.Empty;
    }
}
