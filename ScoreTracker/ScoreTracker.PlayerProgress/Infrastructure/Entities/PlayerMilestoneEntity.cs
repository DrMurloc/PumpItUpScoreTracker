using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities;

/// <summary>
///     Session-level milestone capture: Pumbility gains, Singles/Doubles competitive
///     gains, title completions, paragon gains, and folder lamps. None of these facts
///     were persisted with a timestamp before this table (PlayerHistory has no Pumbility
///     column; UserTitle has no acquisition date) — capture-now-or-lose-it.
/// </summary>
[Index(nameof(UserId), nameof(MixId), nameof(OccurredAt))]
internal sealed class PlayerMilestoneEntity
{
    [Key] public Guid Id { get; set; }

    [Required] public Guid UserId { get; set; }

    [Required] public Guid MixId { get; set; }

    public Guid? SessionId { get; set; }

    [Required] public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Milestone kind name — see <c>MilestoneKind</c> in PlayerProgress contracts.</summary>
    [Required]
    [MaxLength(32)]
    public string Kind { get; set; } = string.Empty;

    public double? OldValue { get; set; }

    public double? NewValue { get; set; }

    [MaxLength(128)] public string? Title { get; set; }

    /// <summary>Compact kind payload, e.g. "D23", "D20|SSS", "S18|UltimateGame".</summary>
    [MaxLength(64)]
    public string? Detail { get; set; }
}
