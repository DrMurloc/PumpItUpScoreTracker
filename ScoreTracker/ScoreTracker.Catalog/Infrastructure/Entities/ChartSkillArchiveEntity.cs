using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Catalog.Infrastructure.Entities
{
    /// <summary>
    ///     One-time snapshot of the hand-maintained ChartSkill rows, written by the
    ///     PiuCenterAliasAndMetrics migration before the crawler took ownership of
    ///     ChartSkill. Never read by the app — kept purely as a just-in-case archive.
    /// </summary>
    internal sealed class ChartSkillArchiveEntity
    {
        [Key] public Guid Id { get; set; }
        public Guid ChartId { get; set; }
        [Required] [MaxLength(64)] public string SkillName { get; set; } = string.Empty;
        public bool IsHighlighted { get; set; }
        public DateTimeOffset ArchivedAt { get; set; }
    }
}
