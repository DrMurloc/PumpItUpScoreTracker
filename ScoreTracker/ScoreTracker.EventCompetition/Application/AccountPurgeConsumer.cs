using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.EventCompetition.Domain;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     Deletes a purged account's tournament sessions, registrations, roles, and photo
///     verifications. Idempotent — the purge event re-fires daily for a week.
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
