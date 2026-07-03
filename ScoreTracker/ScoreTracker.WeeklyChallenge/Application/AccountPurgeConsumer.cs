using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.WeeklyChallenge.Domain;

namespace ScoreTracker.WeeklyChallenge.Application;

/// <summary>
///     Deletes a purged account's weekly-challenge entries and placements. Idempotent — the
///     purge event re-fires daily for a week.
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
