using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.EventCompetition.Infrastructure.Entities
{
    /// <summary>
    ///     One board of a season — a (mix, chart type) pair with its frozen serialized scoring
    ///     configuration (docs/design/march-of-murlocs.md §6). ScoringConfig stores the same
    ///     TournamentConfigurationJsonEntity shape the legacy Tournament.Configuration column
    ///     held, so historical sessions re-price byte-identically. Legacy boards keep their
    ///     legacy tournament's Guid, which is what keeps every old URL resolving.
    /// </summary>
    internal sealed class MoMBoardEntity
    {
        [Key] public Guid Id { get; set; }
        [Required] public Guid SeasonId { get; set; }
        [Required] public Guid MixId { get; set; }
        [Required] public byte ChartType { get; set; }
        [Required] public string ScoringConfig { get; set; } = string.Empty;
    }
}
