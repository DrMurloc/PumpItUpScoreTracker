namespace ScoreTracker.Domain.Models;

/// <summary>
///     One peer, whoever they turn out to be: a PIU Scores account, or a player who exists only on
///     the official board (docs/design/pumbility-overhaul.md D59, peers-abstraction.md D37).
///     <para>
///         Everything that counts peers counts these — how many passed a chart, who is above you,
///         who is on the roster — so the two kinds cannot be double-counted or silently dropped by
///         a surface that only knows about one of them. It is the same shape
///         <c>RivalSubject</c> gives a board-only rival, made available to every peer source
///         instead of only that one.
///     </para>
///     <para>
///         Equality is the identity: two voices are the same peer when they name the same account,
///         or the same board row. A person who owns several board rows was folded into one voice by
///         the mirror before this type ever saw them, and is named by the row it kept.
///     </para>
/// </summary>
public readonly record struct PeerVoice
{
    private PeerVoice(Guid? userId, int boardPlayerId, string? tag)
    {
        UserId = userId;
        BoardPlayerId = boardPlayerId;
        Tag = tag;
    }

    /// <summary>The account, when this peer has one the site may speak for.</summary>
    public Guid? UserId { get; }

    /// <summary>The board row this peer is read from, when they have no such account.</summary>
    public int BoardPlayerId { get; }

    /// <summary>The public tag a board peer is named by. Null for an account, which has its own name.</summary>
    public string? Tag { get; }

    /// <summary>True when the mirror is the only place this peer exists.</summary>
    public bool IsFromBoard => UserId == null;

    public static PeerVoice Account(Guid userId)
    {
        return new PeerVoice(userId, 0, null);
    }

    public static PeerVoice FromBoard(int boardPlayerId, string tag)
    {
        return new PeerVoice(null, boardPlayerId, tag);
    }

    public override string ToString()
    {
        return UserId?.ToString() ?? $"board:{BoardPlayerId}";
    }
}
