using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ScoreTracker.PlayerProgress.Infrastructure.Entities;

/// <summary>
///     One score batch's significant wins, keyed by the event that produced them. The
///     <see cref="Payload" /> is a JSON list of Contracts.SignificantWin, written whole and read
///     whole.
///     <para>
///         The event id is the PRIMARY KEY rather than a column, which is what makes the write
///         idempotent: a redelivered bus message or a re-run backfill collides instead of
///         duplicating. (UserId, MixId, OccurredAt) is the feed's seek — it serves both audiences,
///         a community's member set and a player's rival list, because neither is in the key.
///     </para>
/// </summary>
[Index(nameof(UserId), nameof(MixId), nameof(OccurredAt))]
[Index(nameof(OccurredAt))]
internal sealed class PlayerHighlightEntity
{
    [Key] public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public Guid MixId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? SessionId { get; set; }
    [Required] public string Payload { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
}
