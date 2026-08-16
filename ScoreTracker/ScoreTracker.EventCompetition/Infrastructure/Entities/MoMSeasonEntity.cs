using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    /// <summary>
    ///     A March of Murlocs season (docs/design/march-of-murlocs.md §6). Year/Quarter are
    ///     NULL for the off-grid legacy seasons; the filtered unique index over them is the
    ///     anti-runaway guarantee (D2) — a duplicate quarterly season cannot exist.
    /// </summary>
    internal sealed class MoMSeasonEntity
    {
        [Key] public Guid Id { get; set; }
        public int? Year { get; set; }
        public byte? Quarter { get; set; }
        [Required] [MaxLength(100)] public string Name { get; set; } = string.Empty;
        [Required] public DateTimeOffset StartsAt { get; set; }
        [Required] public DateTimeOffset EndsAt { get; set; }
        [Required] public DateTimeOffset CreatedAt { get; set; }
    }
}
