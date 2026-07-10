using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities;

/// <summary>
///     One computed season recap per user per mix — the JSON payload is the
///     PlayerRecap contract, written whole by the recap saga and read whole by
///     the recap page. Composite key configured in PlayerProgressModelContribution.
/// </summary>
internal sealed class PlayerSeasonRecapEntity
{
    public Guid UserId { get; set; }

    public Guid MixId { get; set; }

    [Required] public string Payload { get; set; } = string.Empty;

    [Required] public int SchemaVersion { get; set; }

    [Required] public DateTimeOffset ComputedAt { get; set; }
}
