namespace ScoreTracker.Rivals.Domain;

/// <summary>
///     Whether one player may draw an arrow at another (docs/design/rivals.md D9–D16). Pure, so
///     every basis is pinned by a DomainTest rather than by whichever handler happens to call it.
///     <para>
///         Evaluated ONCE, at add time. The edge is the consent from then on: going private does
///         not sever rivals and neither does leaving a shared community, which is what spares the
///         system a dormancy state machine and a reconciliation job nobody would notice was broken.
///         The counterweight is the reverse list — it is the only revocation there is.
///     </para>
/// </summary>
internal static class RivalVisibilityPolicy
{
    public static RivalAddVerdict CanAdd(RivalAddCandidate candidate)
    {
        if (candidate.IsSelf) return new RivalAddVerdict(false, RivalAddRefusal.Self, null);
        if (candidate.IsBlockedEitherWay) return new RivalAddVerdict(false, RivalAddRefusal.Blocked, null);

        // A board-only player has no account, so there is nobody whose privacy could refuse.
        // A tag that DOES resolve to an account arrives with TargetUserId set and is judged as
        // that account — which is exactly how "you can't add a private player off the boards"
        // stops needing a rule of its own (D10).
        if (candidate.TargetUserId == null)
            return new RivalAddVerdict(true, RivalAddRefusal.None, RivalAddBasis.BoardOnly);

        if (candidate.TargetIsPublic)
            return new RivalAddVerdict(true, RivalAddRefusal.None, RivalAddBasis.Public);
        if (candidate.SharesCommunity)
            return new RivalAddVerdict(true, RivalAddRefusal.None, RivalAddBasis.SharedCommunity);
        if (candidate.RedeemedInviteCode)
            return new RivalAddVerdict(true, RivalAddRefusal.None, RivalAddBasis.InviteCode);

        return new RivalAddVerdict(false, RivalAddRefusal.NotVisible, null);
    }
}

/// <summary>
///     Everything the decision needs, already looked up. <paramref name="TargetUserId" /> null
///     means the target is a board tag with no account behind it.
/// </summary>
internal sealed record RivalAddCandidate(
    Guid? TargetUserId,
    bool TargetIsPublic,
    bool SharesCommunity,
    bool RedeemedInviteCode,
    bool IsBlockedEitherWay,
    bool IsSelf);

internal sealed record RivalAddVerdict(bool Allowed, RivalAddRefusal Refusal, RivalAddBasis? Basis);

/// <summary>Why an add was refused. Only <see cref="Self" /> is ever worth saying out loud.</summary>
internal enum RivalAddRefusal
{
    None,
    Self,

    /// <summary>
    ///     Reported to the user as plain unavailability. Telling somebody they have been blocked
    ///     turns a quiet control into a confrontation, and the block's whole point is that it
    ///     needs no conversation.
    /// </summary>
    Blocked,

    NotVisible
}

internal enum RivalAddBasis
{
    Public,
    SharedCommunity,
    InviteCode,
    BoardOnly
}
