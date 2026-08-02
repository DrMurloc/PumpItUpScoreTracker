using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Rivals.Contracts.Commands;
using ScoreTracker.Rivals.Domain;

namespace ScoreTracker.Rivals.Application;

/// <summary>
///     The four mutations on the rival graph. Feature-grouped rather than one class per command:
///     they share the same current-user and repository dependencies and read as one story.
/// </summary>
internal sealed class RivalSaga :
    IRequestHandler<AddRivalCommand, Guid>,
    IRequestHandler<RemoveRivalCommand>,
    IRequestHandler<BlockRivalCommand>,
    IRequestHandler<UnblockRivalCommand>
{
    private readonly RivalAdder _adder;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IRivalRepository _rivals;

    public RivalSaga(IRivalRepository rivals, RivalAdder adder, ICurrentUserAccessor currentUser,
        IDateTimeOffsetAccessor dateTime)
    {
        _rivals = rivals;
        _adder = adder;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public Task<Guid> Handle(AddRivalCommand request, CancellationToken cancellationToken)
    {
        return _adder.Add(_currentUser.User.Id, request.TargetUserId, request.TargetTag, viaInviteCode: false,
            cancellationToken);
    }

    /// <summary>
    ///     Removable from either end: your own roster drops an arrow you drew, the reverse list
    ///     drops one drawn at you. Anyone else's edge is nobody's business, so the ownership check
    ///     covers both and refuses the rest.
    /// </summary>
    public async Task Handle(RemoveRivalCommand request, CancellationToken cancellationToken)
    {
        var me = _currentUser.User.Id;
        var edge = await _rivals.GetEdge(request.EdgeId, cancellationToken);
        if (edge == null) return;
        if (edge.OwnerUserId != me && edge.TargetUserId != me) throw new NotAuthorizedException("remove this rival");

        await _rivals.Remove(request.EdgeId, cancellationToken);
    }

    public Task Handle(BlockRivalCommand request, CancellationToken cancellationToken)
    {
        var me = _currentUser.User.Id;
        // Blocking yourself would delete your own outgoing edges as a side effect, which is a
        // strange way to discover a typo.
        if (request.BlockedUserId == me) throw new RivalNotAvailableException();

        return _rivals.Block(me, request.BlockedUserId, _dateTime.Now, cancellationToken);
    }

    public Task Handle(UnblockRivalCommand request, CancellationToken cancellationToken)
    {
        return _rivals.Unblock(_currentUser.User.Id, request.BlockedUserId, cancellationToken);
    }
}
