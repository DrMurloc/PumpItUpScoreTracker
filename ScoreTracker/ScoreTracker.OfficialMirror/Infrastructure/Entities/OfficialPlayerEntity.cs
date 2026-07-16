using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.OfficialMirror.Infrastructure.Entities;

/// <summary>
///     Dimension row for one board-visible player per mix. UserId links the tag to a site
///     account when a score import confirms the identity; unlinked players render from the
///     mirrored tag and avatar alone.
/// </summary>
internal sealed class OfficialPlayerEntity
{
    [Key] public int Id { get; set; }
    public Guid MixId { get; set; }
    [MaxLength(100)] public string Username { get; set; } = string.Empty;
    [MaxLength(400)] public string? AvatarUrl { get; set; }
    public Guid? UserId { get; set; }
    [MaxLength(20)] public string UserIdSource { get; set; } = "None";
    public DateTimeOffset LastSeenAt { get; set; }
}
