using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.Randomizer.Domain;

namespace ScoreTracker.Randomizer.Application;

/// <summary>
///     Deletes the Randomizer's rows for a purged account: the player's rolling draw and their
///     saved randomizer settings. Idempotent — the purge event re-fires daily for a week.
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
