using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.HomePage.Domain;

namespace ScoreTracker.HomePage.Application;

/// <summary>
///     Deletes the dashboard for a purged account: the player's pages and the widget instances
///     laid out on them. Idempotent — the purge event re-fires daily for a week.
/// </summary>
internal sealed class AccountPurgeConsumer : IConsumer<AccountPurgeStartedEvent>
{
    private readonly IAccountPurgeRepository _purge;

    public AccountPurgeConsumer(IAccountPurgeRepository purge)
    {
        _purge = purge;
    }

    public Task Consume(ConsumeContext<AccountPurgeStartedEvent> context)
    {
        return _purge.DeleteAllForUser(context.Message.RetiredUserId, context.CancellationToken);
    }
}
