using MassTransit;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.CommunityTools.Application;

/// <summary>
///     Deletes a purged account's tools, shares and sharing preference. Idempotent — the purge event
///     re-fires daily for a week so a process death mid-purge self-heals.
/// </summary>
internal sealed class AccountPurgeConsumer : IConsumer<AccountPurgeStartedEvent>
{
    private readonly IAccountPurgeRepository _purge;

    public AccountPurgeConsumer(IAccountPurgeRepository purge)
    {
        _purge = purge;
    }

    public async Task Consume(ConsumeContext<AccountPurgeStartedEvent> context)
    {
        await _purge.DeleteAllForUser(context.Message.RetiredUserId, context.CancellationToken);
    }
}
