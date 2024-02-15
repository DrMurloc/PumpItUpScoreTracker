using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Data.Persistence.Entities
{
    public sealed class SkillEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public string Category { get; set; } = string.Empty;
        [Required] public string Color { get; set; } = string.Empty;
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public string Description { get; set; } = string.Empty;
    }
}
