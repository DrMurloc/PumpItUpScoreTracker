using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Catalog.Infrastructure.Entities
{
    [Index(nameof(ChartId))]
    [Index(nameof(SkillName))]
    internal sealed class ChartSkillEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid ChartId { get; set; }
        [Required] [MaxLength(64)] public string SkillName { get; set; } = string.Empty;
        public bool IsHighlighted { get; set; }
    }
}
