using MassTransit;
using ScoreTracker.ChartComments.Domain;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.ChartComments.Application;

/// <summary>
///     Erases a purged account's comments, notes and votes. Idempotent — the purge event re-fires
///     daily for a week, and a second pass finds nothing left to key on.
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
