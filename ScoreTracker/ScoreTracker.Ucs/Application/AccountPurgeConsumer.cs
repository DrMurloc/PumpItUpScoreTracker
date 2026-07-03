using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.Ucs.Domain;

namespace ScoreTracker.Ucs.Application;

/// <summary>
///     Deletes a purged account's UCS leaderboard entries and chart tags. Idempotent — the
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
