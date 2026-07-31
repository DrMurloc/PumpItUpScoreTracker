using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.Identity.Domain;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Application;

/// <summary>
///     Self-serve account deletion. Soft-first, on the same machinery the account merge already
///     uses: the account is hidden immediately, the purge runs after the window, and the whole
///     chain past that point — AccountPurgeStartedEvent, every vertical's consumer, the week of
///     idempotent re-fires — is reused untouched (docs/design/delete-my-data.md §8).
/// </summary>
internal sealed class AccountDeletionHandlers(
        IAccountDeletionRepository deletions,
        IUserRepository users,
        ICommunityReader communities,
        IDateTimeOffsetAccessor dateTime)
    : IRequestHandler<RequestAccountDeletionCommand, AccountDeletionResult>,
        IRequestHandler<CancelAccountDeletionCommand>,
        IRequestHandler<GetPendingAccountDeletionQuery, PendingAccountDeletion?>,
        IRequestHandler<GetAccountDeletionBlockersQuery, IReadOnlyList<OwnedCommunityRecord>>
{
    /// <summary>The grace window. Long enough to change your mind, short enough to mean it.</summary>
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    public async Task<AccountDeletionResult> Handle(RequestAccountDeletionCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await deletions.GetPending(request.UserId, cancellationToken);
        if (existing != null)
            return new AccountDeletionResult(AccountDeletionOutcome.AlreadyScheduled, existing.PurgeAfter);

        // A community is other people's. The creator hands it over themselves rather than having
        // the system pick an heir — asked through the published port because Identity must not
        // reference Communities, which already references Identity.
        var owned = (await communities.GetOwnedCommunities(request.UserId, cancellationToken)).ToArray();
        if (owned.Length > 0)
            return new AccountDeletionResult(AccountDeletionOutcome.BlockedByOwnedCommunities,
                OwnedCommunities: owned);

        var user = await users.GetUser(request.UserId, cancellationToken);
        if (user == null) return new AccountDeletionResult(AccountDeletionOutcome.Scheduled);

        var now = dateTime.Now;
        await deletions.Save(new AccountDeletionRequest(Guid.NewGuid(), request.UserId, now,
            now + GracePeriod, null, null, user.IsPublic, user.GameTag?.ToString()), cancellationToken);

        // Hidden right away: out of the leaderboards and off the game tag, so nobody meets a
        // ghost during the window. The snapshot above is what puts it back.
        await users.SaveUser(user with { IsPublic = false, GameTag = null, ClaimsInvalidatedAt = now },
            cancellationToken);

        return new AccountDeletionResult(AccountDeletionOutcome.Scheduled, now + GracePeriod);
    }

    public async Task Handle(CancelAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        var pending = await deletions.GetPending(request.UserId, cancellationToken);
        if (pending == null) return;

        await deletions.Save(pending with { CancelledAt = dateTime.Now }, cancellationToken);

        var user = await users.GetUser(request.UserId, cancellationToken);
        if (user == null) return;

        await users.SaveUser(user with
        {
            IsPublic = pending.WasPublic,
            GameTag = pending.GameTag == null ? null : Name.From(pending.GameTag)
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<OwnedCommunityRecord>> Handle(GetAccountDeletionBlockersQuery request,
        CancellationToken cancellationToken)
    {
        return (await communities.GetOwnedCommunities(request.UserId, cancellationToken)).ToArray();
    }

    public async Task<PendingAccountDeletion?> Handle(GetPendingAccountDeletionQuery request,
        CancellationToken cancellationToken)
    {
        var pending = await deletions.GetPending(request.UserId, cancellationToken);
        return pending == null ? null : new PendingAccountDeletion(pending.RequestedAt, pending.PurgeAfter);
    }
}
