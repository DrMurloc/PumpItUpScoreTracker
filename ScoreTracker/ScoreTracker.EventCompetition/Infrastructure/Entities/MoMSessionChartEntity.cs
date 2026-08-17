using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    /// <summary>
    ///     One chart of a session, normalized out of the legacy JSON blob
    ///     (docs/design/march-of-murlocs.md §6) — a board render no longer deserializes per
    ///     player, and PlayedAt has a home once timestamps land (Slice 3). Rows cascade with
    ///     their session.
    /// </summary>
    internal sealed class MoMSessionChartEntity
    {
        public Guid SessionId { get; set; }
        public int Ordinal { get; set; }
        [Required] public Guid ChartId { get; set; }
        [Required] public int Score { get; set; }
        [Required] [MaxLength(20)] public string Plate { get; set; } = string.Empty;
        [Required] public bool IsBroken { get; set; }
        [Required] public int SessionScore { get; set; }
        [Required] public int BonusPoints { get; set; }
        public DateTimeOffset? PlayedAt { get; set; }
    }
}
