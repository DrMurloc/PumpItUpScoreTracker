using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Rivals.Infrastructure.Entities;

/// <summary>
///     One code per user, keyed by the user (docs/design/rivals.md D25 — no multi-code
///     management). Recycling overwrites <see cref="Code" /> in place: the old link stops
///     working, and edges already made with it survive, because revoking a person is the
///     reverse list's job (D24).
/// </summary>
internal sealed class RivalInviteCodeEntity
{
    [Key] public Guid UserId { get; set; }

    [MaxLength(20)] public string Code { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
