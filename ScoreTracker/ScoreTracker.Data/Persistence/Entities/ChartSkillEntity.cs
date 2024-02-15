using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.Data.Persistence.Entities
{
    [Index(nameof(ChartId))]
    public sealed class ChartSkillEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid ChartId { get; set; }
        [Required] public Guid SkillId { get; set; }
    }
}
