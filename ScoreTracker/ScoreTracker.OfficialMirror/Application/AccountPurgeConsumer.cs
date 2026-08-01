using MassTransit;
using ScoreTracker.Domain.Events;
using ScoreTracker.OfficialMirror.Domain;

namespace ScoreTracker.OfficialMirror.Application;

/// <summary>
///     Severs a purged account's link to the mirrored official leaderboards. The mirror itself
///     is public piugame data and survives — only the pointer back to a site account goes, so a
///     deleted player stops being identifiable on boards we merely reflect.
///     Idempotent — the purge event re-fires daily for a week.
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
        return _purge.UnlinkUser(context.Message.RetiredUserId, context.CancellationToken);
    }
}
