using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     The one path an edge is ever created by, shared between the picker and the invite-code
///     redemption. Both have to run the same gate; the only difference is that redemption arrives
///     already carrying its consent.
/// </summary>
internal sealed class RivalAdder
{
    private readonly RivalAudienceReader _audience;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IMediator _mediator;
    private readonly IRivalRepository _rivals;
    private readonly IUserReader _users;

    public RivalAdder(IRivalRepository rivals, IUserReader users, IMediator mediator,
        RivalAudienceReader audience, IDateTimeOffsetAccessor dateTime)
    {
        _rivals = rivals;
        _users = users;
        _mediator = mediator;
        _audience = audience;
        _dateTime = dateTime;
    }

    public async Task<Guid> Add(Guid ownerUserId, Guid? targetUserId, string? targetTag, bool viaInviteCode,
        CancellationToken cancellationToken)
    {
        if (targetUserId == null && string.IsNullOrWhiteSpace(targetTag))
            throw new RivalNotAvailableException();

        // Normalize the target BEFORE anything judges it. A tag that already belongs to an
        // account is stored as the account (D4), which is what stops the same human occupying
        // both columns — and what makes "a private player can't be added off the boards" fall out
        // of the ordinary privacy rule instead of needing one of its own.
        var (resolvedUserId, resolvedTag) =
            await ResolveTarget(targetUserId, targetTag, cancellationToken);

        var candidate = await BuildCandidate(ownerUserId, resolvedUserId, viaInviteCode, cancellationToken);
        var verdict = RivalVisibilityPolicy.CanAdd(candidate);
        if (!verdict.Allowed) throw new RivalNotAvailableException();

        // An idempotent re-add returns the edge that already exists rather than colliding on the
        // unique index — pressing Add twice is a double click, not an error.
        var existing = (await _rivals.GetRivalsOwnedBy(ownerUserId, cancellationToken))
            .FirstOrDefault(e => resolvedUserId != null
                ? e.TargetUserId == resolvedUserId
                : string.Equals(e.TargetTag, resolvedTag, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing.Id;

        var edge = new RivalEdge(Guid.NewGuid(), ownerUserId, resolvedUserId, resolvedTag, _dateTime.Now);
        await _rivals.Add(edge, cancellationToken);
        return edge.Id;
    }

    /// <summary>
    ///     A tag resolves through the mirror, which owns tag normalization — Rivals never
    ///     normalizes one itself (D7), because two normalizers drift. An unknown tag is refused
    ///     outright: there is nothing to point at.
    /// </summary>
    private async Task<(Guid? UserId, string? Tag)> ResolveTarget(Guid? targetUserId, string? targetTag,
        CancellationToken cancellationToken)
    {
        if (targetUserId != null) return (targetUserId, null);

        // The tag lives on the boards, which are per mix; Phoenix is where the site's population
        // is, and a tag that exists on either resolves the same person.
        var resolved = await _mediator.Send(new ResolveOfficialPlayerQuery(MixEnum.Phoenix, targetTag!),
                           cancellationToken)
                       ?? await _mediator.Send(new ResolveOfficialPlayerQuery(MixEnum.Phoenix2, targetTag!),
                           cancellationToken);
        if (resolved == null) throw new RivalNotAvailableException();

        return resolved.LinkedUserId != null ? (resolved.LinkedUserId, null) : (null, resolved.Tag);
    }

    private async Task<RivalAddCandidate> BuildCandidate(Guid ownerUserId, Guid? resolvedUserId,
        bool viaInviteCode, CancellationToken cancellationToken)
    {
        if (resolvedUserId == null)
            return new RivalAddCandidate(null, false, false, viaInviteCode, false, false);

        var targetUserId = resolvedUserId.Value;
        if (targetUserId == ownerUserId)
            return new RivalAddCandidate(targetUserId, false, false, false, false, true);

        var blocked = await _rivals.IsBlockedEitherWay(ownerUserId, targetUserId, cancellationToken);
        var target = await _users.GetUser(targetUserId, cancellationToken);
        // Only ask the community question when the answer could matter: a public target is
        // already addable, and the union read walks every club the caller belongs to.
        var sharesCommunity = target is { IsPublic: false } && !viaInviteCode
                              && (await _audience.GetClubmates(cancellationToken)).Contains(targetUserId);

        return new RivalAddCandidate(targetUserId, target?.IsPublic ?? false, sharesCommunity, viaInviteCode,
            blocked, false);
    }
}
