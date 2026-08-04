using System.ComponentModel.DataAnnotations;

namespace ScoreTracker.Rivals.Infrastructure.Entities;

/// <summary>
///     One directed arrow: <see cref="OwnerUserId" /> picked somebody to measure against.
///     Exactly one target is set (docs/design/rivals.md D1) — <see cref="TargetUserId" /> for a
///     player found on piuscores, <see cref="TargetTag" /> for a board-only player who has no
///     account here.
///     <para>
///         A tag is only ever stored when it does NOT already resolve to a site user (D4), so
///         the same human can never occupy both columns. When their first import links the tag,
///         <c>OfficialPlayerLinkSaga</c> rewrites the row to the user id and the ghost is gone.
///     </para>
/// </summary>
internal sealed class RivalEntity
{
    [Key] public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public Guid? TargetUserId { get; set; }

    /// <summary>
    ///     A board tag, normalized by OfficialMirror before it ever reaches this column — Rivals
    ///     never normalizes one itself (D7), because two normalizers drift.
    /// </summary>
    [MaxLength(100)]
    public string? TargetTag { get; set; }

    public DateTimeOffset AddedAt { get; set; }
}
