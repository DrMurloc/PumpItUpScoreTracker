namespace ScoreTracker.Rivals.Infrastructure.Entities;

/// <summary>
///     A block is symmetric (docs/design/rivals.md D15): neither party can rival the other, so
///     every check reads this table in both directions and the row is stored once, from the
///     blocker's side. Blocking also deletes whatever edges already existed.
///     <para>
///         Only ever user↔user. A board-only player has no account and can rival nobody, so
///         there is nothing to block (D16).
///     </para>
/// </summary>
internal sealed class RivalBlockEntity
{
    public Guid UserId { get; set; }

    public Guid BlockedUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
