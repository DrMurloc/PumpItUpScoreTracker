namespace ScoreTracker.Domain.Models;

/// <summary>
///     One peer, whoever they turn out to be: a PIU Scores account, a player the official board is
///     the only record of, or a board-only rival somebody added by tag
///     (docs/design/pumbility-overhaul.md D59, peers-abstraction.md D37).
///     <para>
///         Everything that counts peers counts these — how many passed a chart, who is above you,
///         who is on the roster — so the three kinds cannot be double-counted or silently dropped
///         by a surface that only knows about one of them. Before this type the peer sets were
///         keyed on a user id and a board player had nowhere to be; the ghost rival got by on the
///         accident that its edge id is also a Guid.
///     </para>
///     <para>
///         Equality is the identity: two voices are the same peer when they name the same account,
///         the same board row, or the same rival edge. A person who owns several board rows was
///         folded into one voice by the mirror before this type ever saw them, and is named by the
///         row it kept.
///     </para>
/// </summary>
public readonly record struct PeerVoice
{
    private PeerVoice(Guid? userId, Guid? rivalEdgeId, int boardPlayerId, string? tag)
    {
        UserId = userId;
        RivalEdgeId = rivalEdgeId;
        BoardPlayerId = boardPlayerId;
        Tag = tag;
    }

    /// <summary>The account, when this peer has one the site may speak for.</summary>
    public Guid? UserId { get; }

    /// <summary>The rival edge, when this peer is somebody's board-only rival.</summary>
    public Guid? RivalEdgeId { get; }

    /// <summary>The board row this peer is read from, when the mirror is all there is.</summary>
    public int BoardPlayerId { get; }

    /// <summary>The public tag a board peer is named by. Null for an account, which has its own name.</summary>
    public string? Tag { get; }

    /// <summary>True when this peer's scores can only come from the weekly official board.</summary>
    public bool IsFromBoard => UserId == null;

    public static PeerVoice Account(Guid userId)
    {
        return new PeerVoice(userId, null, 0, null);
    }

    public static PeerVoice FromBoard(int boardPlayerId, string tag)
    {
        return new PeerVoice(null, null, boardPlayerId, tag);
    }

    /// <summary>
    ///     A board-only rival, keyed on the edge rather than the board row: the roster is a list of
    ///     edges, and two players who added the same tag each own their own.
    /// </summary>
    public static PeerVoice RivalGhost(Guid rivalEdgeId, string tag)
    {
        return new PeerVoice(null, rivalEdgeId, 0, tag);
    }

    public override string ToString()
    {
        if (UserId is { } user) return user.ToString();
        return RivalEdgeId is { } edge ? $"edge:{edge}" : $"board:{BoardPlayerId}";
    }
}
