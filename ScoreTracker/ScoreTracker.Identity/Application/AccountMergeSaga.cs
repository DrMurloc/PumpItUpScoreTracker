using MassTransit;
using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Identity.Domain;

namespace ScoreTracker.Identity.Application;

/// <summary>
///     The account-merge feature grouping (a "Saga" per ADR-001 Q8): execute, undo, and the
///     grace-window queries. The retired account is hidden, never deleted here — deletion is
///     the purge pipeline's job after the grace window.
/// </summary>
internal sealed class AccountMergeSaga :
    IRequestHandler<ExecuteAccountMergeCommand, AccountMergeRecord>,
    IRequestHandler<UndoAccountMergeCommand>,
    IRequestHandler<GetActiveAccountMergesQuery, IEnumerable<AccountMergeRecord>>
{
    private static readonly TimeSpan GraceWindow = TimeSpan.FromDays(30);

    private readonly IBus _bus;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IMergeRequestRepository _merges;
    private readonly IUserRepository _users;

    public AccountMergeSaga(ICurrentUserAccessor currentUser, IUserRepository users,
        IMergeRequestRepository merges, IBus bus, IDateTimeOffsetAccessor dateTime)
    {
        _currentUser = currentUser;
        _users = users;
        _merges = merges;
        _bus = bus;
        _dateTime = dateTime;
    }

    public async Task<AccountMergeRecord> Handle(ExecuteAccountMergeCommand request,
        CancellationToken cancellationToken)
    {
        if (request.SurvivorUserId == request.RetiredUserId)
            throw new InvalidAccountMergeException("An account cannot merge with itself.");

        var me = _currentUser.User.Id;
        if (me != request.SurvivorUserId && me != request.RetiredUserId)
            throw new InvalidAccountMergeException("Only a participant can execute a merge.");

        var survivor = await _users.GetUser(request.SurvivorUserId, cancellationToken)
                       ?? throw new UserNotFoundException(request.SurvivorUserId);
        var retired = await _users.GetUser(request.RetiredUserId, cancellationToken)
                      ?? throw new UserNotFoundException(request.RetiredUserId);

        if ((await _merges.GetActiveInvolving(survivor.Id, cancellationToken)).Any() ||
            (await _merges.GetActiveInvolving(retired.Id, cancellationToken)).Any())
            throw new InvalidAccountMergeException("One of the accounts is already inside a merge grace window.");

        var movedLogins = (await _users.GetExternalLogins(retired.Id, cancellationToken)).ToArray();
        foreach (var login in movedLogins)
        {
            await _users.RemoveExternalLogin(retired.Id, login.LoginProviderName, login.ExternalId,
                cancellationToken);
            await _users.CreateExternalLogin(survivor.Id, login.LoginProviderName, login.ExternalId,
                cancellationToken);
        }

        var now = _dateTime.Now;
        var snapshot = new RetiredUserSnapshot(retired.IsPublic, retired.GameTag?.ToString());
        await _users.SaveUser(retired with { IsPublic = false, GameTag = null, ClaimsInvalidatedAt = now },
            cancellationToken);

        var merge = new MergeRequest(Guid.NewGuid(), survivor.Id, retired.Id, movedLogins, snapshot,
            MergeState.Active, now, now + GraceWindow, null);
        await _merges.Save(merge, cancellationToken);

        await _bus.Publish(new AccountsMergedEvent(survivor.Id, retired.Id), cancellationToken);

        return new AccountMergeRecord(merge.Id, merge.SurvivorUserId, merge.RetiredUserId, merge.CreatedAt,
            merge.PurgeAfter);
    }

    public async Task Handle(UndoAccountMergeCommand request, CancellationToken cancellationToken)
    {
        var merge = await _merges.Get(request.MergeRequestId, cancellationToken)
                    ?? throw new InvalidAccountMergeException("Merge not found.");
        if (merge.State != MergeState.Active)
            throw new InvalidAccountMergeException("Only a merge inside its grace window can be undone.");
        if (_currentUser.User.Id != merge.SurvivorUserId)
            throw new InvalidAccountMergeException("Only the surviving account can undo a merge.");

        foreach (var login in merge.MovedLogins)
        {
            await _users.RemoveExternalLogin(merge.SurvivorUserId, login.LoginProviderName, login.ExternalId,
                cancellationToken);
            await _users.CreateExternalLogin(merge.RetiredUserId, login.LoginProviderName, login.ExternalId,
                cancellationToken);
        }

        var retired = await _users.GetUser(merge.RetiredUserId, cancellationToken)
                      ?? throw new UserNotFoundException(merge.RetiredUserId);
        await _users.SaveUser(
            retired with { IsPublic = merge.Snapshot.IsPublic, GameTag = merge.Snapshot.GameTag },
            cancellationToken);

        await _merges.Save(merge with { State = MergeState.Undone }, cancellationToken);
    }

    public async Task<IEnumerable<AccountMergeRecord>> Handle(GetActiveAccountMergesQuery request,
        CancellationToken cancellationToken)
    {
        return (await _merges.GetActiveInvolving(_currentUser.User.Id, cancellationToken))
            .Select(m => new AccountMergeRecord(m.Id, m.SurvivorUserId, m.RetiredUserId, m.CreatedAt, m.PurgeAfter))
            .ToArray();
    }
}
