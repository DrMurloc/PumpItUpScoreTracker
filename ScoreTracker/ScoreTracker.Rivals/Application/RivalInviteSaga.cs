using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Commands;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Rivals.Domain;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     The invite code's whole life: minted on first look, recycled on demand, redeemed by
///     somebody else (docs/design/rivals.md D23–D25).
/// </summary>
internal sealed class RivalInviteSaga :
    IRequestHandler<GetMyRivalInviteCodeQuery, string?>,
    IRequestHandler<RecycleRivalInviteCodeCommand, string>,
    IRequestHandler<GetRivalInvitePreviewQuery, RivalInvitePreviewRecord?>,
    IRequestHandler<RedeemRivalInviteCodeCommand, Guid>
{
    /// <summary>
    ///     32^12 makes a collision a curiosity rather than a risk, but the unique index is the
    ///     real backstop and this is how many times we humour it before giving up.
    /// </summary>
    private const int GenerationAttempts = 5;

    private readonly RivalAdder _adder;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IRivalInviteCodeRepository _invites;
    private readonly IRandomNumberGenerator _random;
    private readonly IRivalRepository _rivals;
    private readonly IUserReader _users;

    public RivalInviteSaga(IRivalInviteCodeRepository invites, IRivalRepository rivals, IUserReader users,
        ICurrentUserAccessor currentUser, IRandomNumberGenerator random, IDateTimeOffsetAccessor dateTime,
        RivalAdder adder)
    {
        _invites = invites;
        _rivals = rivals;
        _users = users;
        _currentUser = currentUser;
        _random = random;
        _dateTime = dateTime;
        _adder = adder;
    }

    /// <summary>
    ///     Null for a public account. They are findable in the picker, so a code would be a step
    ///     that changes nothing — and a control that changes nothing reads as one you missed.
    ///     Minted lazily so a private account that never opens the page never gets a row.
    /// </summary>
    public async Task<string?> Handle(GetMyRivalInviteCodeQuery request, CancellationToken cancellationToken)
    {
        var me = _currentUser.User;
        if (me.IsPublic) return null;

        var existing = await _invites.GetCodeFor(me.Id, cancellationToken);
        return existing ?? await Mint(me.Id, cancellationToken);
    }

    public async Task<string> Handle(RecycleRivalInviteCodeCommand request, CancellationToken cancellationToken)
    {
        return await Mint(_currentUser.User.Id, cancellationToken);
    }

    public async Task<RivalInvitePreviewRecord?> Handle(GetRivalInvitePreviewQuery request,
        CancellationToken cancellationToken)
    {
        if (!RivalInviteCode.TryParse(request.Code, out var code)) return null;

        var ownerId = await _invites.GetUserForCode(code, cancellationToken);
        if (ownerId == null) return null;

        var owner = await _users.GetUser(ownerId.Value, cancellationToken);
        if (owner == null) return null;

        // A code you already redeemed should say so rather than offering the same button again —
        // the landing page is reached by clicking a link, and links get clicked twice.
        var already = _currentUser.IsLoggedIn &&
                      await _rivals.EdgeExists(_currentUser.User.Id, ownerId.Value, null, cancellationToken);

        return new RivalInvitePreviewRecord(owner.Id, owner.Name.ToString(), owner.ProfileImage, already);
    }

    public async Task<Guid> Handle(RedeemRivalInviteCodeCommand request, CancellationToken cancellationToken)
    {
        if (!RivalInviteCode.TryParse(request.Code, out var code))
            throw new InvalidRivalInviteCodeException("It doesn't look like a code.");

        var ownerId = await _invites.GetUserForCode(code, cancellationToken);
        if (ownerId == null) throw new InvalidRivalInviteCodeException("It has expired or been replaced.");

        // The code IS the consent — it is the only thing that makes a private stranger addable,
        // and it is spent here rather than stored, because the edge it creates is the record.
        return await _adder.Add(_currentUser.User.Id, ownerId.Value, null, viaInviteCode: true,
            cancellationToken);
    }

    private async Task<string> Mint(Guid userId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < GenerationAttempts; attempt++)
        {
            var candidate = RivalInviteCode.Generate(_random);
            if (await _invites.TrySetCode(userId, candidate, _dateTime.Now, cancellationToken))
                return candidate;
        }

        throw new InvalidOperationException("Could not mint an unused rival invite code.");
    }
}
