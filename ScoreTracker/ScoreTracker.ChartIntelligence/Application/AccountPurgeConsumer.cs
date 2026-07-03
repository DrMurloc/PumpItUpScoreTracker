using MassTransit;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Events;

namespace ScoreTracker.ChartIntelligence.Application;

/// <summary>
///     Deletes a purged account's difficulty/preference/co-op votes. Idempotent — the purge
///     event re-fires daily for a week.
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
