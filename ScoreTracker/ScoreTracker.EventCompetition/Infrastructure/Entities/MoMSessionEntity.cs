using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    /// <summary>
    ///     A recorded MoM session (docs/design/march-of-murlocs.md §6). PublishedAt NULL means
    ///     draft (D17); once set it is the recorded date and the tie-break clock (D18).
    ///     Everything below PublishedAt is a derived cache of the session's MoMSessionChart
    ///     rows — recomputed on every save, never edited independently. There is deliberately
    ///     no unique key on (BoardId, UserId): a player may run a season many times and boards
    ///     rank sessions, not players (D16). RestTime is in ticks.
    /// </summary>
    internal sealed class MoMSessionEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid BoardId { get; set; }
        [Required] public Guid UserId { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        [Required] public int TotalScore { get; set; }
        [Required] public int ChartsPlayed { get; set; }
        [Required] public long RestTime { get; set; }
        [Required] public double AverageDifficulty { get; set; }
        [Required] public double AverageGrade { get; set; }
        [Required] public byte LowestLevel { get; set; }
        [Required] public byte HighestLevel { get; set; }
        [MaxLength(500)] public string? VideoUrl { get; set; }
        [Required] public DateTimeOffset CreatedAt { get; set; }
        [Required] public DateTimeOffset UpdatedAt { get; set; }
    }
}
